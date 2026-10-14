# LocalHub — Backend

Backend do **LocalHub**, um marketplace de serviços locais ao domicílio (grupo G14, LDS 2026/2027).
É uma única aplicação ASP.NET Core que expõe uma API REST partilhada pelos frontends web e mobile.

| | |
|---|---|
| Linguagem / framework | C# 14 / ASP.NET Core 10 |
| Base de dados | PostgreSQL 18, acedida com EF Core (Npgsql) |
| Testes | xUnit e `Microsoft.AspNetCore.Mvc.Testing` |
| Ramo principal | `master` (protegido: as alterações só entram através de um Pull Request) |

## Pré-requisitos

| Ferramenta | Versão | Verificação |
|---|---|---|
| .NET SDK | 10.0.401 ou uma versão 10.0.x posterior (fixada em `global.json`) | `dotnet --list-sdks` |
| PostgreSQL | 18 | `psql --version` |
| Git | qualquer versão recente | `git --version` |

IDE recomendado: JetBrains Rider. Cliente de base de dados: pgAdmin 4.

## Estrutura da solução

```
LocalHub.slnx
├── src/
│   ├── LocalHub.Api             endpoints REST, autenticação, raiz de composição (Program.cs)
│   ├── LocalHub.Application     casos de uso (serviços)
│   ├── LocalHub.Infrastructure  EF Core / PostgreSQL e sistemas externos
│   └── LocalHub.Domain          entidades e regras de negócio
└── tests/
    ├── LocalHub.UnitTests
    └── LocalHub.IntegrationTests
```

Dependências entre projetos (nunca podem apontar no sentido contrário, e o `Domain`
não referencia nada):

```
Api  →  Application  →  Infrastructure  →  Domain
```

Cada camada regista os seus serviços num ficheiro `DependencyInjection.cs`
(`AddApplication` chama `AddInfrastructure`); o `Program.cs` só chama `AddApplication`.

## Configuração da base de dados

1. Executar uma vez, como administrador do PostgreSQL (por exemplo na Query Tool do pgAdmin, ligado como `postgres`), para criar o utilizador da aplicação com uma palavra-passe à escolha:

   ```sql
   CREATE ROLE localhub WITH LOGIN PASSWORD 'escolher-uma-palavra-passe';
   ```

2. Criar a base de dados `localhub_dev`, com dono `localhub`, com o esquema e os dados fictícios de referência (scripts e contas de referência em [`database/README.md`](database/README.md)). A partir da raiz do repositório (no Windows, o `psql` está em `C:\Program Files\PostgreSQL\18\bin`):

   ```powershell
   psql -U postgres -d postgres -f database/criar-base-dados.sql   # primeira vez
   psql -U postgres -d postgres -f database/repor-base-dados.sql   # repor os dados de referência

Usar apenas **dados fictícios** em todos os ambientes.

## Configuração

Os segredos nunca são submetidos no repositório. O ficheiro de configuração real é ignorado pelo Git.

1. Copiar `src/LocalHub.Api/appsettings.Development.example.json` para
   `src/LocalHub.Api/appsettings.Development.json`.
2. Na cópia, substituir `CHANGE_ME` pela palavra-passe escolhida acima.

Em alternativa, definir a cadeia de ligação através de uma variável de ambiente (PowerShell):

```powershell
$env:ConnectionStrings__LocalHub = "Host=localhost;Port=5432;Database=localhub_dev;Username=localhub;Password=<palavra-passe>"
```

## Compilar, testar e formatar

Executar a partir da raiz do repositório:

```powershell
dotnet build                          # tem de terminar com 0 avisos e 0 erros
dotnet test                           # executa os testes unitários e de integração
dotnet format --verify-no-changes     # não pode reportar alterações
```

Os testes de integração atuais não precisam de base de dados.

## Executar

```powershell
dotnet run --project src/LocalHub.Api --launch-profile http
```

A API escuta em `http://localhost:5041` (apenas HTTP, por opção).

Para verificar se a aplicação consegue aceder à base de dados, abrir
`http://localhost:5041/api/health`: responde `Healthy` quando a ligação funciona e
`Unhealthy` (HTTP 503) quando não funciona.

No ambiente Development, o documento OpenAPI está disponível em
`http://localhost:5041/openapi/v1.json`.

## Collection Postman

`postman/m3-collection-postman-v01.json` tem uma pasta por funcionalidade, com um pedido por endpoint e
verificações do código HTTP e dos campos principais (Plano de Testes do backend v01, secção 12). Com a API em
execução, importar o ficheiro no Postman e executar a collection por ordem, ou, com o Postman CLI:

```powershell
postman collection run postman/m3-collection-postman-v01.json
```

O pedido de início de sessão (E02) guarda o token na variável `token`, usada pelos pedidos protegidos. Cada
execução regista uma conta nova com um e-mail fictício (`@localhub.test`), por isso pode ser repetida.

## Integração contínua

Cada push e cada Pull Request para `master` executa dois workflows do GitHub Actions
(`.github/workflows/`). A `master` só aceita alterações cujos checks passem.

### `ci.yml` — quality gates (RI v02, secção 10.6)

| Gate | Verificação | Comando / ferramenta | Evidência |
|---|---|---|---|
| QG-05 | Formatação e analisadores | `dotnet format --verify-no-changes` | log do job |
| QG-01 | Construção | `dotnet build -c Release` | log do job |
| QG-02 | Testes unitários | `dotnet test tests/LocalHub.UnitTests` | relatório TRX |
| QG-03 | Testes de integração (serviço PostgreSQL 18) | `dotnet test tests/LocalHub.IntegrationTests` | relatório TRX |
| QG-04 | Cobertura de linhas | Coverlet + ReportGenerator + `.github/scripts/check-coverage.py` | relatório de cobertura |

O incumprimento de qualquer gate faz a pipeline falhar e bloqueia a integração.

**Cobertura (QG-04).** Os limiares são verificados separadamente para dois conjuntos de classes:

- módulos críticos (os listados em `.github/critical-modules.txt`): no mínimo 50 %;
- restantes módulos (todas as outras classes): no mínimo 20 %.

Cada linha de `.github/critical-modules.txt` é um namespace ou o nome completo de uma classe; uma
classe é crítica se o seu nome completo for igual ao prefixo ou começar por `<prefixo>.`. Um prefixo
sem classes (módulo ainda por implementar, ou nome mal escrito) só produz um aviso no log do passo
*QG-04*; o gate falha apenas quando a cobertura de um dos conjuntos fica abaixo do limiar. O Revisor
deve confirmar que um aviso se deve a um módulo por implementar. Os módulos críticos correspondem à
secção 11.4 da Especificação de Requisitos.

O código gerado (`obj/`, `*.generated.cs`) e as migrações ficam excluídos. Para alterar quais são
os módulos críticos, editar `.github/critical-modules.txt`.

**Artefactos.** Cada execução guarda o artefacto `evidencias-backend` (relatórios TRX e de cobertura).
Descarregá-lo na página da execução, em *Artifacts*.

### `codeql.yml` — análise de segurança

O CodeQL (C#, conjunto de consultas `security-extended`, configurado em
`.github/codeql/codeql-config.yml`) corre em cada push e Pull Request para `master` e todas as
segundas-feiras. Os alertas de severidade alta ou crítica bloqueiam a integração. Os resultados
estão em *Security → Code scanning*.

### Configuração necessária no repositório

- Secret de Actions `TEST_DB_PASSWORD`: uma palavra-passe aleatória (só letras e números).
  Serve para criar o serviço PostgreSQL descartável de cada execução; não é uma credencial real.
- Ruleset da `master` que exige um Pull Request, os checks `backend` e `Analyze (csharp)` e os
  resultados do code scanning do CodeQL.

## Trabalhar no projeto

Regras do regulamento interno do grupo (RI v02, secção 9):

- Nunca fazer push para `master`. Criar um ramo por Issue: `<tipo>/i<número>-<descrição-curta>`,
  por exemplo `feature/i036-estrutura-base`.
- Mensagens de commit: `<tipo>: <descrição-curta> [I<número>]`, com tipo `feat`, `fix`, `test`,
  `docs`, `refactor`, `chore` ou `wip`.
- Abrir um Pull Request com a ligação para a Issue, um resumo, como testar, os critérios de
  aceitação e os testes executados. O Executor não pode aprovar nem integrar o seu próprio Pull
  Request; o merge é um squash merge feito pelo Revisor depois da autorização do Auditor.
- Antes de abrir um Pull Request, `dotnet build`, `dotnet test` e
  `dotnet format --verify-no-changes` têm de passar.

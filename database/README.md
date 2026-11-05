# LocalHub — Scripts das bases de dados v1

Issues **I038** (scripts v1, escritos à mão), **I083** (esquema gerado a partir das migrações) e **I057**
(scripts v1 gerados a partir das migrações EF, idempotentes, com conjunto de dados de referência completo).

Scripts SQL que criam e repõem a base de dados **PostgreSQL 18** do LocalHub e carregam o conjunto de
dados fictícios de referência (RNF-19, RNF-20). O esquema **é gerado a partir das migrações do Entity
Framework Core** e é por isso igual ao aplicado por `dotnet ef database update` (CCA OF-M3-003).

## 1. Ficheiros

| Ficheiro | Conteúdo |
|---|---|
| `criar-base-dados.sql` | Cria a base `localhub_dev`, se não existir, aplica as migrações em falta e carrega os dados de referência numa base sem contas. **Idempotente.** **Ponto de entrada.** |
| `repor-base-dados.sql` | Elimina a base `localhub_dev` (terminando as ligações abertas) e volta a criá-la com o `criar-base-dados.sql`. **Ponto de entrada.** |
| `01-esquema.sql` | Esquema **gerado** com `dotnet ef migrations script --idempotent` (secção 5). Inclui a tabela `__EFMigrationsHistory`, para que a API reconheça a base como atualizada. Não editar à mão. |
| `02-dados-referencia.sql` | Dados fictícios de referência (secção 4). |

Os ficheiros `01-` e `02-` são incluídos pelos pontos de entrada (`\ir`) e não se executam sozinhos.
Os quatro ficheiros têm de estar na mesma pasta.

## 2. Pré-requisitos

- PostgreSQL Server 18 instalado e em execução (RI v02, secção 10.2).
- `psql` disponível (vem com o PostgreSQL; no Windows fica em `C:\Program Files\PostgreSQL\18\bin`).
- Utilizador com permissão para criar bases de dados (por exemplo, `postgres`).
- Utilizador `localhub`, com que o backend se liga (README do backend). É criado uma vez, por cada aluno,
  com uma palavra-passe escolhida por ele e que nunca entra no repositório:

  ```sql
  CREATE ROLE localhub WITH LOGIN PASSWORD 'escolher-uma-palavra-passe';
  ```

  Se não existir, os scripts param com `role "localhub" does not exist`.
- Só para gerar de novo o `01-esquema.sql`: .NET SDK 10 e a ferramenta `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

## 3. Comandos

Os comandos podem ser executados a partir de qualquer pasta. Ligam-se à base `postgres` porque a base
`localhub_dev` é criada ou eliminada pelo próprio script. A base, os tipos, as tabelas e os dados ficam com
dono `localhub`, para que o backend os possa ler e alterar.

**Criar** — num servidor sem a base, cria-a com o esquema e os dados de referência. Sobre uma base já criada
não altera o esquema nem os dados e não gera erros (só aplica migrações que ainda não estejam aplicadas):

```sh
psql -U postgres -d postgres -f <pasta>/criar-base-dados.sql
```

**Repor** — **apaga todos os dados** e volta ao conjunto de referência:

```sh
psql -U postgres -d postgres -f <pasta>/repor-base-dados.sql
```

Para outro servidor ou porta, acrescentar `-h <servidor> -p <porta>`. A palavra-passe do utilizador do
PostgreSQL é pedida pelo `psql` ou lida da variável `PGPASSWORD`; nunca é guardada no repositório (RNF-04).

Para usar outra base ou outro dono, acrescentar `-v dbname=<base>` e/ou `-v owner=<utilizador>`:

```sh
psql -U postgres -d postgres -v dbname=localhub_demo -f <pasta>/repor-base-dados.sql
```

Não usar a base `localhub_test`: os testes de integração apagam e recriam o esquema dessa base em cada execução.

Comportamento:

- Os scripts param no primeiro erro (`ON_ERROR_STOP`).
- O esquema e os dados são aplicados numa única transação; se algo falhar, nada fica aplicado.
- Os dados de referência só são carregados se a tabela `User` estiver vazia; numa base já com dados, o
  `criar-base-dados.sql` não os altera e indica que deve ser usado o `repor-base-dados.sql`.
- A base é criada em UTF-8 com o fornecedor de locale `builtin` (`C.UTF-8`), que se comporta da mesma forma
  em Windows e em Linux.

## 4. Conjunto de dados de referência

Todos os dados são fictícios (RNF-20): os e-mails usam o domínio reservado `.test`, as moradas têm nomes
fictícios e as coordenadas são aproximadas ao centro das localidades. As palavras-passe estão guardadas
apenas como resumo criptográfico com sal (RNF-01), no formato V3 do `PasswordHasher` do ASP.NET Core
Identity (PBKDF2-HMAC-SHA512, 100 000 iterações, sal aleatório de 16 bytes); contas com a mesma
palavra-passe têm resumos diferentes.

Os identificadores são fixos, para poderem ser usados na collection Postman e nos testes manuais. As
sequências das colunas de identidade continuam a partir do maior identificador (o primeiro utilizador criado
pela API recebe o `userId` 14).

### 4.1 Categorias de serviços ao domicílio (RNF-24)

| Id | Categoria | Estado |
|---:|---|---|
| 1 | Canalização | Ativa |
| 2 | Eletricidade | Ativa |
| 3 | Jardinagem | Ativa |
| 4 | Limpeza doméstica | Ativa |
| 5 | Pintura | Ativa |
| 6 | Climatização | **Inativa** (F003: categorias inativas não podem ser escolhidas) |

### 4.2 Contas

| Id | Nome | E-mail | Papel | Estado | Localização | Palavra-passe |
|---:|---|---|---|---|---|---|
| 1 | Ana Almeida | ana.almeida@localhub.test | Administrador | Ativa | — | `Admin2026` |
| 2 | Rui Rocha | rui.rocha@localhub.test | Administrador | Ativa | — | `Admin2026` |
| 3 | Carla Costa | carla.costa@localhub.test | Cliente | Ativa | Porto | `Demo2026` |
| 4 | Daniel Duarte | daniel.duarte@localhub.test | Cliente | Ativa | Felgueiras | `Demo2026` |
| 5 | Eva Esteves | eva.esteves@localhub.test | Cliente | Ativa | Guimarães | `Demo2026` |
| 6 | Fábio Faria | fabio.faria@localhub.test | Cliente | Ativa | **Sem localização** | `Demo2026` |
| 7 | Gina Gomes | gina.gomes@localhub.test | Cliente | **Desativada** | Braga | `Demo2026` |
| 8 | Hugo Henriques | hugo.henriques@localhub.test | Prestador | Ativa | Porto | `Demo2026` |
| 9 | Inês Igreja | ines.igreja@localhub.test | Prestador | Ativa | Felgueiras | `Demo2026` |
| 10 | João Jardim | joao.jardim@localhub.test | Prestador | Ativa | Penafiel | `Demo2026` |
| 11 | Lúcia Lopes | lucia.lopes@localhub.test | Prestador | Ativa | Guimarães | `Demo2026` |
| 12 | Nuno Neves | nuno.neves@localhub.test | Prestador | **Desativada** | Porto | `Demo2026` |
| 13 | Olga Oliveira | olga.oliveira@localhub.test | Prestador | Ativa | **Sem localização** | `Demo2026` |

Estas palavras-passe são fictícias e servem só para ambientes de desenvolvimento e de demonstração.

### 4.3 Perfis profissionais

| Id | Prestador | Raio (km) | Categorias (áreas de atuação) | Classificação média |
|---:|---|---:|---|---:|
| 1 | Hugo Henriques | 25 | Canalização, Eletricidade | 5,0 |
| 2 | Inês Igreja | 30 | Jardinagem, Limpeza doméstica | — |
| 3 | João Jardim | 40 | Pintura, Canalização | 2,0 |
| 4 | Lúcia Lopes | 15 | Limpeza doméstica | 4,0 |
| 5 | Nuno Neves | 20 | Canalização | — (conta desativada: não aparece nas pesquisas) |

A Olga Oliveira não tem perfil profissional (F004, F006). A classificação média é a média das avaliações dos
serviços do Prestador, com uma casa decimal, como em `ProfessionalProfile.CalculateAverageRating`.

### 4.4 Serviços

| Id | Cliente | Categoria | Modelo | Estado | Propostas e solicitações diretas | Avaliação |
|---:|---|---|---|---|---|---|
| 1 | Carla | Canalização | Público | Aberto | Propostas 1 (Hugo, 85 €) e 2 (João, 95 €) **pendentes** | — |
| 2 | Carla | Eletricidade | Solicitação direta | Aberto | Solicitação 1 ao Hugo **pendente** | — |
| 3 | Daniel | Jardinagem | Solicitação direta | Atribuído | Solicitação 2 à Inês respondida; proposta 3 aceite | — |
| 4 | Carla | Canalização | Público | Realizado | Proposta 4 (Hugo) aceite; proposta 5 (João) recusada | 5 |
| 5 | Eva | Limpeza doméstica | Público | Realizado | Proposta 6 (Lúcia) aceite; proposta 7 (Inês) recusada | 4 |
| 6 | Daniel | Limpeza doméstica | Solicitação direta | Realizado | Solicitação 3 à Inês respondida; proposta 8 aceite | **por avaliar** |
| 7 | Eva | Pintura | Público | Cancelado (pelo Cliente, em aberto) | Proposta 9 (João) anulada | — |
| 8 | Carla | Eletricidade | Público | Cancelado (pelo Prestador, atribuído) | Proposta 10 (Hugo) aceite | — |
| 9 | Daniel | Canalização | Solicitação direta | Aberto | Solicitação 4 ao João **recusada** | — |
| 10 | Eva | Pintura | Público | Realizado | Proposta 11 (João) aceite | 2 |
| 11 | Gina (desativada) | Jardinagem | Público | Aberto | — (não aceita propostas) | — |

Os serviços abertos têm a data de início pedida em 2027, para continuarem válidos durante o projeto. Cada
mudança de estado está registada no histórico (12 registos), com o utilizador que a fez; a criação do serviço
não é registada como mudança de estado.

### 4.5 Totais esperados após a criação ou a reposição

| Tabela | Linhas |
|---|---:|
| `ServiceCategory` | 6 |
| `User` | 13 (2 Administradores, 5 Clientes, 6 Prestadores) |
| `ProfessionalProfile` | 5 |
| `ServiceArea` | 8 |
| `Service` | 11 |
| `DirectRequest` | 4 |
| `Proposal` | 11 |
| `Review` | 3 |
| `ServiceStatusHistory` | 12 |
| `UserSession` | 0 |
| `__EFMigrationsHistory` | 1 por migração (atualmente 1: `20261027121052_InitialCreate`) |

## 5. Esquema gerado a partir das migrações

O esquema da base de dados é definido pelas migrações do Entity Framework Core
(`src/LocalHub.Infrastructure/Migrations`). O `01-esquema.sql` é gerado a partir delas, a partir do ramo
`master`, e inclui todas as migrações. Sempre que for criada uma migração nova, gerar o ficheiro outra vez, a
partir da raiz do repositório:

```sh
dotnet ef migrations script --idempotent --no-transactions -p src/LocalHub.Infrastructure -s src/LocalHub.Api -o database/01-esquema.sql
```

- `--idempotent`: cada migração só é aplicada se ainda não estiver registada em `__EFMigrationsHistory`, por
  isso o script pode ser executado mais de uma vez e atualiza bases criadas com migrações anteriores.
- `--no-transactions`: o `criar-base-dados.sql` já aplica o esquema e os dados numa única transação.

O `dotnet ef` grava o ficheiro com BOM e fins de linha CRLF no Windows. O `psql` não aceita o BOM no início de
um ficheiro incluído, por isso, depois de gerar, remover o BOM e converter para LF (por exemplo, em Git Bash):

```sh
sed -i '1s/^\xEF\xBB\xBF//; s/\r$//' database/01-esquema.sql
```

Para confirmar que o ficheiro está atualizado, gerá-lo para outro ficheiro e comparar: não deve haver diferenças.

## 6. Registo da execução (evidência)

Execução em 05/11/2026, PostgreSQL 18.6 (x86_64-windows), `dotnet-ef` 10.0.13, commit de base `3b82c88`
(`master`), base `localhub_i057`, dono `localhub`:

1. **Criação** numa instância sem a base: concluída sem erros; 6 + 13 + 5 + 8 + 11 + 4 + 11 + 3 + 12 linhas inseridas.
2. **Segunda execução** do `criar-base-dados.sql`: concluída sem erros; o `pg_dump` (esquema e dados) é igual ao da
   primeira execução.
3. **Comparação com as migrações**: o `pg_dump --schema-only` da base criada pelo script é igual ao de uma base
   criada com `dotnet ef database update`; as duas têm `20261027121052_InitialCreate` em `__EFMigrationsHistory`,
   a única migração do código.
4. **Reposição** com a base alterada (categoria extra, nome alterado, tabela extra) e uma ligação aberta:
   concluída sem erros; o `pg_dump` depois da reposição é igual ao da criação.
5. **RNF-01:** 13 resumos V3 em 13 contas, todos distintos; nenhuma palavra-passe legível.
   **RNF-24:** só as 6 categorias de serviços ao domicílio. **RNF-20:** todos os e-mails em `@localhub.test`.
6. **Coerência:** classificações médias iguais à média das avaliações; nenhum serviço atribuído ou realizado sem
   proposta aceite; nenhuma avaliação de serviço não realizado; o último registo do histórico de cada serviço
   coincide com o seu estado.
7. **API sobre a base criada** (Development): início de sessão com 200 para `ana.almeida` e `rui.rocha`
   (Administradores) e para Cliente e Prestadores; 403 para a conta desativada `gina.gomes`; 401 com
   palavra-passe errada; `GET /api/auth/me`, `/api/accounts`, `/api/reports/client`, `/api/reports/provider` e
   `/api/services/1` com 200.

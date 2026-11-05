# LocalHub — Scripts da base de dados

Issues **I038** (scripts v1) e **I083** (esquema gerado a partir das migrações e conjunto de dados reduzido).

Scripts SQL que criam e repõem a base de dados **PostgreSQL 18** do LocalHub e carregam o conjunto de
dados fictícios de referência (RNF-19, RNF-20).

## 1. Ficheiros

| Ficheiro | Conteúdo |
|---|---|
| `repor-base-dados.sql` | Elimina a base `localhub_dev`, se existir (terminando as ligações abertas), e cria-a de novo com o esquema e os dados de referência. **Ponto de entrada**, tanto para criar como para repor. |
| `01-esquema.sql` | Esquema da base de dados, **gerado a partir das migrações do Entity Framework Core** (secção 5). Inclui a tabela `__EFMigrationsHistory`, para que a API reconheça a base como atualizada. |
| `02-dados-referencia.sql` | Dados fictícios de referência (secção 4). |

Os ficheiros `01-` e `02-` são incluídos pelo `repor-base-dados.sql` (`\ir`) e não se executam sozinhos.
Os três ficheiros têm de estar na mesma pasta.

## 2. Pré-requisitos

- PostgreSQL Server 18 instalado e em execução (RI v02, secção 10.2).
- `psql` disponível (vem com o PostgreSQL; no Windows fica em `C:\Program Files\PostgreSQL\18\bin`).
- Utilizador com permissão para criar bases de dados (por exemplo, `postgres`).
- Utilizador `localhub`, com que o backend se liga (README do backend). É criado uma vez, por cada aluno,
  com uma palavra-passe escolhida por ele e que nunca entra no repositório:

  ```sql
  CREATE ROLE localhub WITH LOGIN PASSWORD 'escolher-uma-palavra-passe';
  ```

  Se não existir, o script para com `role "localhub" does not exist`.

## 3. Comando

O mesmo comando cria a base num servidor onde ainda não existe e repõe-na quando já existe. **Na reposição,
todos os dados são apagados** e a base volta ao conjunto de referência.

```sh
psql -U postgres -d postgres -f <pasta>/repor-base-dados.sql
```

O comando liga-se à base `postgres` porque a base `localhub_dev` é eliminada e criada pelo próprio script.
A base, os tipos, as tabelas e os dados ficam com dono `localhub`, para que o backend os possa ler e alterar.

Para outro servidor ou porta, acrescentar `-h <servidor> -p <porta>`. A palavra-passe do utilizador do
PostgreSQL é pedida pelo `psql` ou lida da variável `PGPASSWORD`; nunca é guardada no repositório (RNF-04).

Para usar outra base ou outro dono, acrescentar `-v dbname=<base>` e/ou `-v owner=<utilizador>`:

```sh
psql -U postgres -d postgres -v dbname=localhub_demo -f <pasta>/repor-base-dados.sql
```

Não usar a base `localhub_test`: os testes de integração apagam e recriam o esquema dessa base em cada execução.

Comportamento:

- O script para no primeiro erro (`ON_ERROR_STOP`).
- O esquema e os dados são carregados numa única transação; se algo falhar, a base fica vazia.
- A base é criada em UTF-8 com o fornecedor de locale `builtin` (`C.UTF-8`), que se comporta da mesma forma
  em Windows e em Linux.

## 4. Conjunto de dados de referência

Todos os dados são fictícios (RNF-20): os e-mails usam o domínio reservado `.test`, as moradas têm nomes
fictícios e as coordenadas são aproximadas ao centro das localidades. As palavras-passe estão guardadas
apenas como resumo criptográfico com sal (RNF-01), no formato do `PasswordHasher` do ASP.NET Core Identity.

### 4.1 Categorias de serviços ao domicílio (RNF-24)

| Categoria | Estado |
|---|---|
| Canalização | Ativa |
| Eletricidade | Ativa |

### 4.2 Contas

| Nome | E-mail | Papel | Localização | Palavra-passe |
|---|---|---|---|---|
| Ana Almeida | ana.almeida@localhub.test | Administrador | — | `Admin2026` |
| Carla Costa | carla.costa@localhub.test | Cliente | Porto | `Demo2026` |
| Hugo Henriques | hugo.henriques@localhub.test | Prestador | Porto | `Demo2026` |

### 4.3 Perfil profissional

| Prestador | Raio (km) | Categorias |
|---|---:|---|
| Hugo Henriques | 25 | Canalização, Eletricidade |

### 4.4 Totais esperados após a criação ou a reposição

| Tabela | Linhas |
|---|---:|
| `ServiceCategory` | 2 |
| `User` | 3 (1 Administrador, 1 Cliente, 1 Prestador) |
| `ProfessionalProfile` | 1 |
| `ServiceArea` | 2 |
| `__EFMigrationsHistory` | 1 |
| Restantes tabelas | 0 |

## 5. Esquema gerado a partir das migrações

O esquema da base de dados é definido pelas migrações do Entity Framework Core
(`src/LocalHub.Infrastructure/Migrations`). O `01-esquema.sql` é gerado a partir delas e não deve ser editado
à mão. Sempre que for criada uma migração nova, gerar o ficheiro outra vez, a partir da raiz do repositório:

```sh
dotnet ef migrations script --no-transactions -p src/LocalHub.Infrastructure -s src/LocalHub.Api -o database/01-esquema.sql
```

A opção `--no-transactions` é necessária porque o `repor-base-dados.sql` já carrega o esquema e os dados numa
única transação.

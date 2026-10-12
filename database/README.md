# LocalHub — Scripts da base de dados v1

Issue **I038 — Elaborar os scripts das bases de dados v1** (m3, Sprint 05).

Scripts SQL que criam e repõem a base de dados **PostgreSQL 18** do LocalHub segundo o
**Modelo de Dados v01** (`m2-modelo-dados-v01.md`) e carregam o conjunto de dados fictícios de
referência (RNF-19, RNF-20).

## 1. Ficheiros

| Ficheiro | Conteúdo |
|---|---|
| `criar-base-dados.sql` | Cria a base de dados `localhub_dev` (dono `localhub`), o esquema e os dados de referência. **Ponto de entrada.** |
| `repor-base-dados.sql` | Elimina a base de dados `localhub_dev` (terminando as ligações abertas) e volta a criá-la. **Ponto de entrada.** |
| `01-esquema.sql` | Tipos enumerados, tabelas, chaves primárias e estrangeiras, restrições `CHECK` e `UNIQUE` e índices. |
| `02-dados-referencia.sql` | Dados fictícios de referência (secção 4). |

Os ficheiros `01-` e `02-` são incluídos pelos pontos de entrada (`\ir`) e não se executam sozinhos.
Os quatro ficheiros têm de estar na mesma pasta.

## 2. Pré-requisitos

- PostgreSQL Server 18 instalado e em execução (RI v02, secção 10.2).
- `psql` disponível (vem com o PostgreSQL; no Windows fica em `C:\Program Files\PostgreSQL\18\bin`).
- Utilizador com permissão para criar bases de dados (por exemplo, `postgres`).
- Utilizador `localhub`, com que o backend se liga (README do backend, «Database setup»). É criado
  uma vez, por cada aluno, com uma palavra-passe escolhida por ele e que nunca entra no repositório:

  ```sql
  CREATE ROLE localhub WITH LOGIN PASSWORD 'escolher-uma-palavra-passe';
  ```

  Se não existir, os scripts param com `role "localhub" does not exist`.
- Sem extensões adicionais (Documentação da Arquitetura v02, DEC-08; RNF-18).

## 3. Comandos

Os comandos podem ser executados a partir de qualquer pasta. Ligam-se à base `postgres` porque a
base `localhub_dev` é criada ou eliminada pelo próprio script. A base, os tipos, as tabelas e os dados
ficam com dono `localhub`, para que o backend os possa ler e alterar.

**Criar** (servidor onde ainda não existe a base `localhub_dev`):

```sh
psql -U postgres -d postgres -f <pasta>/criar-base-dados.sql
```

**Repor** (base já existente e preenchida; **apaga todos os dados** e volta ao conjunto de referência):

```sh
psql -U postgres -d postgres -f <pasta>/repor-base-dados.sql
```

Para outro servidor ou porta, acrescentar `-h <servidor> -p <porta>`. A palavra-passe do utilizador
do PostgreSQL é pedida pelo `psql` ou lida da variável `PGPASSWORD`; nunca é guardada no repositório (RNF-04).

Para usar outra base ou outro dono (por exemplo, uma base separada para testes), acrescentar
`-v dbname=<base>` e/ou `-v owner=<utilizador>`:

```sh
psql -U postgres -d postgres -v dbname=localhub_test -f <pasta>/repor-base-dados.sql
```

Comportamento:

- Os scripts param no primeiro erro (`ON_ERROR_STOP`).
- O esquema e os dados são carregados numa única transação; se algo falhar, a base fica vazia.
- O `criar-base-dados.sql` falha com `database "localhub_dev" already exists` se a base já existir (por exemplo, criada à mão segundo o README do backend). Nesse caso, usar o `repor-base-dados.sql`.
- A base é criada em UTF-8 com o fornecedor de locale `builtin` (`C.UTF-8`), que se comporta da mesma forma em Windows e em Linux.

## 4. Conjunto de dados de referência

Todos os dados são fictícios (RNF-20): os e-mails usam o domínio reservado `.test`, as moradas têm
nomes fictícios e as coordenadas são aproximadas ao centro das localidades.

### 4.1 Categorias de serviços ao domicílio (RNF-24)

| Categoria | Estado |
|---|---|
| Canalização | Ativa |
| Eletricidade | Ativa |
| Jardinagem | Ativa |
| Limpeza doméstica | Ativa |
| Pintura | Ativa |
| Carpintaria | Ativa |
| Reparação de eletrodomésticos | Ativa |
| Serralharia | Ativa |
| Climatização | **Inativa** (para testar F003 e a exclusão de categorias inativas) |

### 4.2 Contas

As palavras-passe estão guardadas apenas como resumo criptográfico com sal (RNF-01), no formato V3 do
`PasswordHasher` do ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 000 iterações, sal aleatório de
16 bytes). O backend deve usar `IPasswordHasher<User>` (Modelo de Classes do backend v01) com as
opções predefinidas para que estas contas iniciem sessão (I040). Contas com a mesma palavra-passe
têm resumos diferentes.

| Nome | E-mail | Papel | Estado | Localização | Palavra-passe |
|---|---|---|---|---|---|
| Ana Almeida | ana.almeida@localhub.test | Administrador | Ativa | — | `Admin2026` |
| Rui Rocha | rui.rocha@localhub.test | Administrador | Ativa | — | `Admin2026` |
| Carla Costa | carla.costa@localhub.test | Cliente | Ativa | Porto | `Demo2026` |
| Daniel Duarte | daniel.duarte@localhub.test | Cliente | Ativa | Felgueiras | `Demo2026` |
| Eva Esteves | eva.esteves@localhub.test | Cliente | Ativa | Guimarães | `Demo2026` |
| Fábio Faria | fabio.faria@localhub.test | Cliente | Ativa | **Sem localização** | `Demo2026` |
| Gina Gomes | gina.gomes@localhub.test | Cliente | **Desativada** | Braga | `Demo2026` |
| Hugo Henriques | hugo.henriques@localhub.test | Prestador | Ativa | Porto | `Demo2026` |
| Inês Igreja | ines.igreja@localhub.test | Prestador | Ativa | Felgueiras | `Demo2026` |
| João Jardim | joao.jardim@localhub.test | Prestador | Ativa | Penafiel | `Demo2026` |
| Lúcia Lopes | lucia.lopes@localhub.test | Prestador | Ativa | Guimarães | `Demo2026` |
| Marco Matos | marco.matos@localhub.test | Prestador | Ativa | Amarante | `Demo2026` |
| Nuno Neves | nuno.neves@localhub.test | Prestador | **Desativada** | Porto | `Demo2026` |
| Olga Oliveira | olga.oliveira@localhub.test | Prestador | Ativa | **Sem localização** | `Demo2026` |
| Paulo Pinto | paulo.pinto@localhub.test | Prestador | Ativa | **Sem localização** | `Demo2026` |

### 4.3 Perfis profissionais

| Prestador | Raio (km) | Categorias | Caso de teste |
|---|---:|---|---|
| Hugo Henriques | 25 | Canalização, Eletricidade | Prestador completo |
| Inês Igreja | 30 | Jardinagem, Limpeza doméstica | Prestador completo |
| João Jardim | 40 | Pintura, Carpintaria | Prestador completo |
| Lúcia Lopes | 15 | Limpeza doméstica | Prestador completo |
| Marco Matos | 50 | Canalização, Reparação de eletrodomésticos | Prestador completo |
| Nuno Neves | 20 | Canalização | Conta desativada: não aparece nas pesquisas (F005) |
| Paulo Pinto | 10 | Eletricidade, Serralharia | Sem localização: não aparece nas pesquisas (F005) |
| Olga Oliveira | — | — | Sem perfil profissional (F004, F006) |

Nenhum perfil tem classificação média, porque o conjunto de referência não inclui serviços nem avaliações.

### 4.4 Totais esperados após a criação ou a reposição

| Tabela | Linhas |
|---|---:|
| `ServiceCategory` | 9 |
| `User` | 15 (2 Administradores, 5 Clientes, 8 Prestadores) |
| `ProfessionalProfile` | 7 |
| `ServiceArea` | 12 |
| Restantes tabelas | 0 |

## 5. Correspondência com o Modelo de Dados v01

- **Nomes:** as tabelas, as colunas e os tipos enumerados têm os nomes do modelo. As tabelas e as colunas são criadas entre aspas, para manter as maiúsculas e minúsculas (`"User"."userId"`). Nas consultas manuais têm de ser escritas também entre aspas. Os nomes das restrições seguem a convenção `PK_`, `FK_`, `UQ_`, `UX_` (índice único), `CK_` e `IX_`.
- **Objetos criados:** 7 tipos enumerados, 13 tabelas, 78 colunas, 13 chaves primárias, 17 chaves estrangeiras, 32 restrições `CHECK` e 32 índices (incluindo os das chaves primárias e das restrições de unicidade).
- **Unicidade sem distinguir maiúsculas de minúsculas:** `User.email` e `ServiceCategory.name` usam índices únicos sobre `lower(...)`.
- **No máximo uma proposta aceite por serviço:** índice único parcial `UX_Proposal_serviceId_accepted`.
- **Validadas no backend, e não na base de dados:** as regras que dependem de outras tabelas ou do momento da operação. Por exemplo: `serviceProviderId` com o papel `PROVIDER`; `clientId` com o papel `CLIENT`; categoria ativa no momento da criação; solicitação `PENDING` antes de uma proposta em `DIRECT_REQUEST`; avaliação só de serviços `COMPLETED`; pelo menos uma área de atuação por perfil; cálculo de `averageRating`.
- **Eliminação:** as contas e as categorias não são apagadas, só desativadas. As chaves estrangeiras para `User`, `ServiceCategory` e `ProfessionalProfile` usam `ON DELETE RESTRICT`. Os registos técnicos dependentes (sessões, registos push, áreas de atuação, destinatários) usam `ON DELETE CASCADE`.

## 6. Registo da execução (evidência)

Execução da v01 em PostgreSQL 18.6 (x86_64-windows), em 11/10/2026 (base `localhub`, dono `postgres`):

1. `criar-base-dados.sql` numa instância vazia: concluído sem erros; 9 + 15 + 7 + 12 linhas inseridas.
2. Verificação do esquema: 7 enumerados, 13 tabelas, 78 colunas, 13 PK, 17 FK, 32 CHECK, 32 índices.
3. RNF-01: 0 palavras-passe legíveis; 15 resumos distintos em 15 contas.
4. Testes de restrições: 19 inserções inválidas rejeitadas pela restrição esperada; inserções válidas aceites.
5. Base alterada (categoria extra, nome alterado, tabela extra) com uma ligação aberta, seguida de `repor-base-dados.sql`: concluído sem erros. O `pg_dump` após a reposição coincide com o da criação, exceto nos valores das sequências, que voltam ao início.
6. `criar-base-dados.sql` com a base já existente: falha, como esperado, com `database "localhub" already exists`.

Execução da v02 em PostgreSQL 18.6 (x86_64-windows), em 12/10/2026 (base `localhub_dev`, dono `localhub`):

1. `repor-base-dados.sql` sobre uma base `localhub_dev` vazia, criada à mão segundo o README do backend: concluído sem erros; 9 + 15 + 7 + 12 linhas inseridas.
2. Verificação do esquema e dos dados: 7 enumerados, 13 tabelas, 78 colunas, 13 PK, 17 FK, 32 CHECK, 32 índices; 0 palavras-passe legíveis e 15 resumos distintos; os 15 resumos verificados com o `PasswordHasher` do ASP.NET Core 10 (`Admin2026` e `Demo2026`).
3. Dono da base, dos tipos e das tabelas: `localhub`; leitura e escrita com o papel `localhub` sem erros de permissão.
4. 8 inserções, alterações ou eliminações inválidas rejeitadas pela restrição esperada.
5. Base alterada e reposta: volta ao conjunto de referência (sem a tabela e a categoria extra; nome original repostos).
6. `criar-base-dados.sql` com `-v owner=inexistente`: para com `role "inexistente" does not exist`, sem criar a base.

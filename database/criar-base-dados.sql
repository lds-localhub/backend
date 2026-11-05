-- =============================================================================
-- LocalHub — Criação da base de dados (PostgreSQL 18)
-- Issues: I038, I083, I057
--
-- Cria a base de dados localhub_dev, se ainda não existir, aplica o esquema gerado
-- a partir das migrações do Entity Framework Core (01-esquema.sql, idempotente) e
-- carrega os dados fictícios de referência (02-dados-referencia.sql) se a base
-- ainda não tiver contas.
--
-- É idempotente: executado sobre uma base já criada, não altera o esquema, não
-- altera os dados e não gera erros. Numa base criada com uma versão anterior das
-- migrações, aplica só as migrações em falta.
--
-- As tabelas, os tipos e os dados ficam com dono localhub, o utilizador com que o
-- backend se liga (README do backend).
--
-- Comando único (a partir de qualquer pasta):
--   psql -U postgres -d postgres -f <caminho>/criar-base-dados.sql
-- Outra base ou outro dono: acrescentar -v dbname=... e/ou -v owner=...
-- =============================================================================

\set ON_ERROR_STOP on
-- os scripts estão em UTF-8 (necessário no Windows, onde a consola usa outra página de código)
\encoding UTF8

-- valores predefinidos (alteráveis com -v dbname=... e -v owner=...)
\if :{?dbname}
\else
    \set dbname localhub_dev
\endif
\if :{?owner}
\else
    \set owner localhub
\endif

-- o utilizador dono tem de existir; é criado uma vez por cada aluno, com a sua
-- palavra-passe (README, secção 2). Se não existir, o script para aqui.
SELECT :'owner'::regrole AS owner_ok \gset

-- cria a base só se ainda não existir
SELECT NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'dbname') AS criar_base \gset
\if :criar_base
    CREATE DATABASE :"dbname"
        WITH OWNER :"owner"
             ENCODING 'UTF8'
             TEMPLATE template0
             LOCALE_PROVIDER builtin
             BUILTIN_LOCALE 'C.UTF-8';
    \echo 'Base de dados' :dbname 'criada.'
\else
    \echo 'A base de dados' :dbname 'já existe; só são aplicadas as migrações em falta.'
\endif

\connect :dbname
\encoding UTF8

-- Esquema e dados numa única transação: se algo falhar, nada fica aplicado.
-- Os objetos são criados com o papel do dono, para que o backend tenha acesso.
BEGIN;
SET LOCAL ROLE :"owner";
\ir 01-esquema.sql

-- os dados de referência só são carregados numa base sem contas
SELECT NOT EXISTS (SELECT 1 FROM "User") AS carregar_dados \gset
\if :carregar_dados
    \ir 02-dados-referencia.sql
    \echo 'Dados de referência carregados.'
\else
    \echo 'A base já tem dados; os dados de referência não foram carregados (usar repor-base-dados.sql para os repor).'
\endif
COMMIT;

\echo 'Base de dados' :dbname 'pronta, com dono' :owner '.'

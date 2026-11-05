-- =============================================================================
-- LocalHub — Reposição da base de dados (PostgreSQL 18)
-- Issues: I038, I083, I057
--
-- Elimina a base de dados localhub_dev, se existir, terminando as ligações abertas,
-- e cria-a de novo com criar-base-dados.sql: esquema gerado a partir das migrações
-- do Entity Framework Core e dados fictícios de referência. TODOS OS DADOS
-- EXISTENTES SÃO APAGADOS.
--
-- Comando único (a partir de qualquer pasta):
--   psql -U postgres -d postgres -f <caminho>/repor-base-dados.sql
-- Outra base ou outro dono: acrescentar -v dbname=... e/ou -v owner=...
-- =============================================================================

\set ON_ERROR_STOP on
\encoding UTF8

\if :{?dbname}
\else
    \set dbname localhub_dev
\endif
\if :{?owner}
\else
    \set owner localhub
\endif

-- confirma que o dono existe antes de apagar a base
SELECT :'owner'::regrole AS owner_ok \gset

DROP DATABASE IF EXISTS :"dbname" WITH (FORCE);
\echo 'Base de dados' :dbname 'eliminada.'

\ir criar-base-dados.sql

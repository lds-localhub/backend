-- =============================================================================
-- LocalHub — Reposição da base de dados (PostgreSQL 18)
-- Issue: I038 — Elaborar os scripts das bases de dados v1
--
-- Elimina a base de dados localhub_dev, se existir, terminando as ligações abertas,
-- e volta a criá-la com o esquema e os dados de referência (criar-base-dados.sql).
-- Todos os dados existentes são apagados.
--
-- Comando único (a partir de qualquer pasta):
--   psql -U postgres -d postgres -f <caminho>/repor-base-dados.sql
-- Outra base ou outro dono: acrescentar -v dbname=... e/ou -v owner=...
-- =============================================================================

\set ON_ERROR_STOP on

\if :{?dbname}
\else
    \set dbname localhub_dev
\endif

DROP DATABASE IF EXISTS :"dbname" WITH (FORCE);

\ir criar-base-dados.sql

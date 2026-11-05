-- =============================================================================
-- LocalHub — Criação e reposição da base de dados (PostgreSQL 18)
-- Issues: I038, I083
--
-- Elimina a base de dados localhub_dev, se existir, terminando as ligações abertas,
-- e cria-a de novo com o esquema (01-esquema.sql) e os dados de referência
-- (02-dados-referencia.sql). Num servidor onde a base ainda não existe, serve para
-- a criar. Todos os dados existentes são apagados.
--
-- As tabelas, os tipos e os dados ficam com dono localhub, o utilizador com que o
-- backend se liga (README do backend).
--
-- Comando único (a partir de qualquer pasta):
--   psql -U postgres -d postgres -f <caminho>/repor-base-dados.sql
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

DROP DATABASE IF EXISTS :"dbname" WITH (FORCE);

CREATE DATABASE :"dbname"
    WITH OWNER :"owner"
         ENCODING 'UTF8'
         TEMPLATE template0
         LOCALE_PROVIDER builtin
         BUILTIN_LOCALE 'C.UTF-8';

\connect :dbname
\encoding UTF8

-- Esquema e dados numa única transação: se algo falhar, a base fica vazia.
-- Os objetos são criados com o papel do dono, para que o backend tenha acesso.
BEGIN;
SET LOCAL ROLE :"owner";
\ir 01-esquema.sql
\ir 02-dados-referencia.sql
COMMIT;

\echo 'Base de dados' :dbname 'criada com dono' :owner 'e com o esquema e os dados de referência.'

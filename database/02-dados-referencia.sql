-- =============================================================================
-- LocalHub — Conjunto de dados fictícios de referência (PostgreSQL 18)
-- Issue: I038 — Elaborar os scripts das bases de dados v1
--
-- Todos os dados são fictícios (RNF-20). Os endereços de e-mail usam o domínio
-- reservado .test e as moradas e coordenadas são aproximadas a centros urbanos.
-- As palavras-passe estão guardadas apenas como resumo criptográfico com sal, no
-- formato V3 do PasswordHasher do ASP.NET Core Identity (PBKDF2-HMAC-SHA512,
-- 100 000 iterações, sal de 16 bytes) — RNF-01. As palavras-passe legíveis das
-- contas de demonstração estão documentadas no README.md.
--
-- Abrange apenas o conjunto de referência pedido na I038: contas de Administrador
-- (RF-F001-05), Clientes, Prestadores com perfil, localização e raio, e categorias
-- de serviços ao domicílio (RNF-24). Não inclui serviços, propostas, avaliações
-- nem notificações.
--
-- Este ficheiro é incluído por criar-base-dados.sql; não é executado sozinho.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Categorias de serviços ao domicílio (RNF-24)
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceCategory" ("name", "status") VALUES
    ('Canalização',                    'ACTIVE'),
    ('Eletricidade',                   'ACTIVE'),
    ('Jardinagem',                     'ACTIVE'),
    ('Limpeza doméstica',              'ACTIVE'),
    ('Pintura',                        'ACTIVE'),
    ('Carpintaria',                    'ACTIVE'),
    ('Reparação de eletrodomésticos',  'ACTIVE'),
    ('Serralharia',                    'ACTIVE'),
    ('Climatização',                   'INACTIVE');

-- -----------------------------------------------------------------------------
-- 2. Contas de utilizador
-- -----------------------------------------------------------------------------

INSERT INTO "User" ("name", "email", "passwordHash", "role", "status", "address", "latitude", "longitude") VALUES
    -- Administradores (RF-F001-05) — palavra-passe Admin2026
    ('Ana Almeida', 'ana.almeida@localhub.test',
     'AQAAAAIAAYagAAAAEJX5LX5O1NJuqDaZs0tjyyWvNPQZF7ylvOEAFx0lDBVkoPdv2Z+DewyBw6YLTImeeQ==',
     'ADMIN', 'ACTIVE', NULL, NULL, NULL),
    ('Rui Rocha', 'rui.rocha@localhub.test',
     'AQAAAAIAAYagAAAAEN4whZPCxpusqeyps1sy7HjypzFenrZK5XZ63rj0T9Y8/q4jVogFMypeiQLvtJFlMQ==',
     'ADMIN', 'ACTIVE', NULL, NULL, NULL),

    -- Clientes — palavra-passe Demo2026
    ('Carla Costa', 'carla.costa@localhub.test',
     'AQAAAAIAAYagAAAAEAyScCcuF3q+Wbia5whIABEldVRVwBuDC2EMsK8MEAJ2PKbwRPUKUvfQOgTA9Zgisg==',
     'CLIENT', 'ACTIVE', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000),
    ('Daniel Duarte', 'daniel.duarte@localhub.test',
     'AQAAAAIAAYagAAAAEAKKi/TsEOkiVw00usQb3xLg3d9d/tXgAAcLIqtK/+KNS5Jz8JDDbqm3JsEZIIvu4A==',
     'CLIENT', 'ACTIVE', 'Avenida Central Fictícia, 45, 4610-001 Felgueiras', 41.364600, -8.198200),
    ('Eva Esteves', 'eva.esteves@localhub.test',
     'AQAAAAIAAYagAAAAEN0HTeWFHkOiY8eFDUxgGNLUiHouYM8mSdjXH86QWHa//pKeDSjoO/wQwym/fFlSyA==',
     'CLIENT', 'ACTIVE', 'Largo do Castelo Fictício, 3, 4810-001 Guimarães', 41.442500, -8.291800),
    ('Fábio Faria', 'fabio.faria@localhub.test',
     'AQAAAAIAAYagAAAAEH2kcDtaWaT2UVOxIixI6/yzUKTqYsgzlyfgJB4Yc96vcsa0KhzDx5cUdvibQ2nSqA==',
     'CLIENT', 'ACTIVE', NULL, NULL, NULL),
    ('Gina Gomes', 'gina.gomes@localhub.test',
     'AQAAAAIAAYagAAAAEJTEMToEY4xU7tFSfOa85B9mFEI+xMhbzPKyDnJWw8aIwzNN/wKJWYt1SH99RPlHqw==',
     'CLIENT', 'INACTIVE', 'Rua do Souto Fictícia, 80, 4700-001 Braga', 41.545400, -8.426500),

    -- Prestadores de Serviços — palavra-passe Demo2026
    ('Hugo Henriques', 'hugo.henriques@localhub.test',
     'AQAAAAIAAYagAAAAEE6YXPZu95qhFzmEy5HqA9kfRo+w4RuOWuBJG4AUrlcfWUmiBeK9jAYrO06OA6yj5w==',
     'PROVIDER', 'ACTIVE', 'Avenida da Boavista Fictícia, 900, 4100-001 Porto', 41.157900, -8.629100),
    ('Inês Igreja', 'ines.igreja@localhub.test',
     'AQAAAAIAAYagAAAAEH6VAl7vljmt189nHCr1LHrbExiZB24S3g6aLlVQ3/CSfDLzzuIKx4jvC4lynS9YYw==',
     'PROVIDER', 'ACTIVE', 'Rua da Estação Fictícia, 7, 4610-002 Felgueiras', 41.368000, -8.195000),
    ('João Jardim', 'joao.jardim@localhub.test',
     'AQAAAAIAAYagAAAAEApNSlaeD0iJpi26bZHbOk25fOUhxZ1ACOGcaCA2AaIdeE9Fr5eaNIYnoXn38novaA==',
     'PROVIDER', 'ACTIVE', 'Rua Direita Fictícia, 21, 4560-001 Penafiel', 41.208300, -8.283300),
    ('Lúcia Lopes', 'lucia.lopes@localhub.test',
     'AQAAAAIAAYagAAAAEHD6IrG/ywKwekh5nDjG+WMR0fUt/y4q6NIC/ltYk1AvRCKF7VxzoN7h1maZb1zaOg==',
     'PROVIDER', 'ACTIVE', 'Rua de Santa Maria Fictícia, 15, 4810-002 Guimarães', 41.444000, -8.296000),
    ('Marco Matos', 'marco.matos@localhub.test',
     'AQAAAAIAAYagAAAAEDh5IKm+KwCZWvd41HAEkaOKzrtMddGeZcA0ucooYKXX3GD/cI177kZcum+gU1F6Gg==',
     'PROVIDER', 'ACTIVE', 'Rua 31 de Janeiro Fictícia, 50, 4600-001 Amarante', 41.271700, -8.075000),
    ('Nuno Neves', 'nuno.neves@localhub.test',
     'AQAAAAIAAYagAAAAEIZwB8u4ZFjEj7PimOY8w77ess2d1IILuUCXh9w4Tnob0N1OD1mhBt1DnnAgu4AROA==',
     'PROVIDER', 'INACTIVE', 'Rua de Cedofeita Fictícia, 200, 4050-002 Porto', 41.153000, -8.620000),
    ('Olga Oliveira', 'olga.oliveira@localhub.test',
     'AQAAAAIAAYagAAAAEI8Y7554UoDEsrcaW2sWWAMl2J/38wLNj24i5r4ZIFBnaqwGMVT7I74Qghr7MctYJg==',
     'PROVIDER', 'ACTIVE', NULL, NULL, NULL),
    ('Paulo Pinto', 'paulo.pinto@localhub.test',
     'AQAAAAIAAYagAAAAENxS5GHSybxpBSv2uC8Ttjg9VVcH8gcg3sEOi6eYpDxk5XITl1fy+RT+Np/KSOZRmQ==',
     'PROVIDER', 'ACTIVE', NULL, NULL, NULL);

-- -----------------------------------------------------------------------------
-- 3. Perfis profissionais (Olga Oliveira fica sem perfil, de propósito)
-- -----------------------------------------------------------------------------

INSERT INTO "ProfessionalProfile"
    ("serviceProviderId", "introduction", "contactDetails", "experience", "averageRating", "serviceRadiusKm")
SELECT u."userId", p.introduction, p.contact_details, p.experience, NULL, p.radius_km
FROM (VALUES
    ('hugo.henriques@localhub.test',
     'Canalizador e eletricista certificado. Reparações e instalações em habitações.',
     'Telefone 900 000 001 (fictício); hugo.henriques@localhub.test',
     '12 anos em canalização e instalações elétricas residenciais.', 25),
    ('ines.igreja@localhub.test',
     'Manutenção de jardins e limpezas domésticas regulares ou pontuais.',
     'Telefone 900 000 002 (fictício); ines.igreja@localhub.test',
     '6 anos em jardinagem e limpeza doméstica.', 30),
    ('joao.jardim@localhub.test',
     'Pintura de interiores e exteriores e pequenos trabalhos de carpintaria.',
     'Telefone 900 000 003 (fictício); joao.jardim@localhub.test',
     '15 anos em pintura e carpintaria de acabamentos.', 40),
    ('lucia.lopes@localhub.test',
     'Limpezas domésticas, incluindo limpezas pós-obra.',
     'Telefone 900 000 004 (fictício); lucia.lopes@localhub.test',
     '4 anos em limpeza doméstica.', 15),
    ('marco.matos@localhub.test',
     'Canalização, desentupimentos e reparação de eletrodomésticos.',
     'Telefone 900 000 005 (fictício); marco.matos@localhub.test',
     '20 anos em canalização e reparação de eletrodomésticos.', 50),
    ('nuno.neves@localhub.test',
     'Canalização em moradias e apartamentos.',
     'Telefone 900 000 006 (fictício); nuno.neves@localhub.test',
     '8 anos em canalização.', 20),
    ('paulo.pinto@localhub.test',
     'Instalações elétricas e serralharia ligeira.',
     'Telefone 900 000 007 (fictício); paulo.pinto@localhub.test',
     '3 anos em eletricidade e serralharia.', 10)
) AS p (email, introduction, contact_details, experience, radius_km)
JOIN "User" u ON u."email" = p.email
ORDER BY u."userId";

-- -----------------------------------------------------------------------------
-- 4. Áreas de atuação (categorias de cada perfil)
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceArea" ("serviceCategoryId", "professionalProfileId")
SELECT c."serviceCategoryId", pp."professionalProfileId"
FROM (VALUES
    ('hugo.henriques@localhub.test', 'Canalização'),
    ('hugo.henriques@localhub.test', 'Eletricidade'),
    ('ines.igreja@localhub.test',    'Jardinagem'),
    ('ines.igreja@localhub.test',    'Limpeza doméstica'),
    ('joao.jardim@localhub.test',    'Pintura'),
    ('joao.jardim@localhub.test',    'Carpintaria'),
    ('lucia.lopes@localhub.test',    'Limpeza doméstica'),
    ('marco.matos@localhub.test',    'Canalização'),
    ('marco.matos@localhub.test',    'Reparação de eletrodomésticos'),
    ('nuno.neves@localhub.test',     'Canalização'),
    ('paulo.pinto@localhub.test',    'Eletricidade'),
    ('paulo.pinto@localhub.test',    'Serralharia')
) AS a (email, category)
JOIN "User" u ON u."email" = a.email
JOIN "ProfessionalProfile" pp ON pp."serviceProviderId" = u."userId"
JOIN "ServiceCategory" c ON c."name" = a.category
ORDER BY pp."professionalProfileId", c."serviceCategoryId";

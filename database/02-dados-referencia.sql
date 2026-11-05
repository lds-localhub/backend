-- =============================================================================
-- LocalHub — Conjunto de dados fictícios de referência (PostgreSQL 18)
-- Issues: I038, I083
--
-- Todos os dados são fictícios (RNF-20). Os endereços de e-mail usam o domínio
-- reservado .test e as moradas e coordenadas são aproximadas a centros urbanos.
-- As palavras-passe estão guardadas apenas como resumo criptográfico com sal, no
-- formato V3 do PasswordHasher do ASP.NET Core Identity (PBKDF2-HMAC-SHA512,
-- 100 000 iterações, sal de 16 bytes) — RNF-01. As palavras-passe legíveis das
-- contas estão documentadas no README.md.
--
-- Conjunto mínimo: um Administrador (RF-F001-05), um Cliente, um Prestador com
-- perfil e duas categorias. Não inclui serviços, propostas nem avaliações.
--
-- Este ficheiro é incluído por repor-base-dados.sql; não é executado sozinho.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Categorias de serviços ao domicílio (RNF-24)
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceCategory" ("name", "status") VALUES
    ('Canalização',  'ACTIVE'),
    ('Eletricidade', 'ACTIVE');

-- -----------------------------------------------------------------------------
-- 2. Contas de utilizador
-- -----------------------------------------------------------------------------

INSERT INTO "User" ("name", "email", "passwordHash", "role", "status", "address", "latitude", "longitude") VALUES
    -- Administrador (RF-F001-05) — palavra-passe Admin2026
    ('Ana Almeida', 'ana.almeida@localhub.test',
     'AQAAAAIAAYagAAAAEJX5LX5O1NJuqDaZs0tjyyWvNPQZF7ylvOEAFx0lDBVkoPdv2Z+DewyBw6YLTImeeQ==',
     'ADMIN', 'ACTIVE', NULL, NULL, NULL),
    -- Cliente — palavra-passe Demo2026
    ('Carla Costa', 'carla.costa@localhub.test',
     'AQAAAAIAAYagAAAAEAyScCcuF3q+Wbia5whIABEldVRVwBuDC2EMsK8MEAJ2PKbwRPUKUvfQOgTA9Zgisg==',
     'CLIENT', 'ACTIVE', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000),
    -- Prestador de Serviços — palavra-passe Demo2026
    ('Hugo Henriques', 'hugo.henriques@localhub.test',
     'AQAAAAIAAYagAAAAEE6YXPZu95qhFzmEy5HqA9kfRo+w4RuOWuBJG4AUrlcfWUmiBeK9jAYrO06OA6yj5w==',
     'PROVIDER', 'ACTIVE', 'Avenida da Boavista Fictícia, 900, 4100-001 Porto', 41.157900, -8.629100);

-- -----------------------------------------------------------------------------
-- 3. Perfil profissional do Prestador
-- -----------------------------------------------------------------------------

INSERT INTO "ProfessionalProfile"
    ("serviceProviderId", "introduction", "contactDetails", "experience", "averageRating", "serviceRadiusKm")
SELECT u."userId",
       'Canalizador e eletricista certificado. Reparações e instalações em habitações.',
       'Telefone 900 000 001 (fictício); hugo.henriques@localhub.test',
       '12 anos em canalização e instalações elétricas residenciais.',
       NULL,
       25
FROM "User" u
WHERE u."email" = 'hugo.henriques@localhub.test';

-- -----------------------------------------------------------------------------
-- 4. Áreas de atuação (categorias do perfil)
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceArea" ("serviceCategoryId", "professionalProfileId")
SELECT c."serviceCategoryId", pp."professionalProfileId"
FROM (VALUES
    ('hugo.henriques@localhub.test', 'Canalização'),
    ('hugo.henriques@localhub.test', 'Eletricidade')
) AS a (email, category)
JOIN "User" u ON u."email" = a.email
JOIN "ProfessionalProfile" pp ON pp."serviceProviderId" = u."userId"
JOIN "ServiceCategory" c ON c."name" = a.category
ORDER BY c."serviceCategoryId";

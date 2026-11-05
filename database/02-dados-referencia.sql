-- =============================================================================
-- LocalHub — Conjunto de dados fictícios de referência (PostgreSQL 18)
-- Issues: I038, I083, I057
--
-- Todos os dados são fictícios (RNF-20). Os endereços de e-mail usam o domínio
-- reservado .test e as moradas e coordenadas são aproximadas a centros urbanos.
-- As palavras-passe estão guardadas apenas como resumo criptográfico com sal, no
-- formato V3 do PasswordHasher do ASP.NET Core Identity (PBKDF2-HMAC-SHA512,
-- 100 000 iterações, sal aleatório de 16 bytes) — RNF-01. As palavras-passe
-- legíveis das contas estão documentadas no README.md.
--
-- Os identificadores são fixos (OVERRIDING SYSTEM VALUE), para que a collection
-- Postman e os testes manuais os possam usar; no fim, as sequências das colunas
-- de identidade avançam para o valor seguinte ao maior identificador.
--
-- Os estados de serviços, propostas, solicitações diretas, histórico e
-- classificações médias são coerentes com as regras do domínio do backend.
--
-- Este ficheiro é incluído por criar-base-dados.sql; não é executado sozinho.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Categorias de serviços ao domicílio (RNF-24)
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceCategory" ("serviceCategoryId", "name", "status") OVERRIDING SYSTEM VALUE VALUES
    (1, 'Canalização',       'ACTIVE'),
    (2, 'Eletricidade',      'ACTIVE'),
    (3, 'Jardinagem',        'ACTIVE'),
    (4, 'Limpeza doméstica', 'ACTIVE'),
    (5, 'Pintura',           'ACTIVE'),
    (6, 'Climatização',      'INACTIVE');   -- categoria inativa (F003)

-- -----------------------------------------------------------------------------
-- 2. Contas de utilizador
--    Administradores: palavra-passe Admin2026. Restantes contas: Demo2026.
-- -----------------------------------------------------------------------------

INSERT INTO "User" ("userId", "name", "email", "passwordHash", "role", "status", "address", "latitude", "longitude")
OVERRIDING SYSTEM VALUE VALUES
    -- Administradores (RF-F001-05)
    (1, 'Ana Almeida', 'ana.almeida@localhub.test',
     'AQAAAAIAAYagAAAAEJX5LX5O1NJuqDaZs0tjyyWvNPQZF7ylvOEAFx0lDBVkoPdv2Z+DewyBw6YLTImeeQ==',
     'ADMIN', 'ACTIVE', NULL, NULL, NULL),
    (2, 'Rui Rocha', 'rui.rocha@localhub.test',
     'AQAAAAIAAYagAAAAEJv70gN78SnYOkpeJoTLr/hZeMTOWequffpHMFIs19eM8PJOin8bjtwc1S3kg4qCuw==',
     'ADMIN', 'ACTIVE', NULL, NULL, NULL),
    -- Clientes
    (3, 'Carla Costa', 'carla.costa@localhub.test',
     'AQAAAAIAAYagAAAAEAyScCcuF3q+Wbia5whIABEldVRVwBuDC2EMsK8MEAJ2PKbwRPUKUvfQOgTA9Zgisg==',
     'CLIENT', 'ACTIVE', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000),
    (4, 'Daniel Duarte', 'daniel.duarte@localhub.test',
     'AQAAAAIAAYagAAAAEC0c63AayerraaBRjWx9++aq8ickx1TXQYC9LVVU67DganGTg4L4XMEksG76ptetJA==',
     'CLIENT', 'ACTIVE', 'Rua do Mosteiro Fictícia, 5, 4610-001 Felgueiras', 41.364500, -8.198000),
    (5, 'Eva Esteves', 'eva.esteves@localhub.test',
     'AQAAAAIAAYagAAAAECTVjkY9HvzXOcclvre9Nzl9PC2bUE/foB20XYvF2UeE6FFG3UuAfTvTYEVPjplouw==',
     'CLIENT', 'ACTIVE', 'Largo do Castelo Fictício, 3, 4800-001 Guimarães', 41.444400, -8.296200),
    (6, 'Fábio Faria', 'fabio.faria@localhub.test',
     'AQAAAAIAAYagAAAAEDDdHvVVsRJXxLy0+BUK8LRp2a/8NB1T6UtILuU5RyMWKsGR1dXTQAR6wG0KIVAsuw==',
     'CLIENT', 'ACTIVE', NULL, NULL, NULL),                                   -- sem localização
    (7, 'Gina Gomes', 'gina.gomes@localhub.test',
     'AQAAAAIAAYagAAAAEIHv2uV4p+S4tzT4golXL/F5hI518zdo8gFpxQmiN3f4ckWUgCALdfzYAJTX7FTruA==',
     'CLIENT', 'INACTIVE', 'Avenida da Sé Fictícia, 40, 4700-001 Braga', 41.545400, -8.426500),
    -- Prestadores de Serviços
    (8, 'Hugo Henriques', 'hugo.henriques@localhub.test',
     'AQAAAAIAAYagAAAAEE6YXPZu95qhFzmEy5HqA9kfRo+w4RuOWuBJG4AUrlcfWUmiBeK9jAYrO06OA6yj5w==',
     'PROVIDER', 'ACTIVE', 'Avenida da Boavista Fictícia, 900, 4100-001 Porto', 41.157900, -8.629100),
    (9, 'Inês Igreja', 'ines.igreja@localhub.test',
     'AQAAAAIAAYagAAAAEEIZNBx7wUDi0dNBP/bHGebGLjA9sIRKcnSXZ1xmSt7ppBEVAiSStnvIIIsxAlNcOA==',
     'PROVIDER', 'ACTIVE', 'Rua da Lixa Fictícia, 22, 4610-002 Felgueiras', 41.368000, -8.194000),
    (10, 'João Jardim', 'joao.jardim@localhub.test',
     'AQAAAAIAAYagAAAAEHEp3PKvYAMKKJrC30AcvjKgWpgvBH2oxG88dK3oOpTew6TsHRIdY4ZLeexX86WVEw==',
     'PROVIDER', 'ACTIVE', 'Rua do Sameiro Fictícia, 8, 4560-001 Penafiel', 41.208300, -8.283300),
    (11, 'Lúcia Lopes', 'lucia.lopes@localhub.test',
     'AQAAAAIAAYagAAAAEAgGMh8GgrjvrPVofRb3kpuyFcbUl8L8ykeaBBzwGuMfo7fFdmzCK0yw6LQSAPjgdQ==',
     'PROVIDER', 'ACTIVE', 'Rua de Santa Maria Fictícia, 15, 4800-002 Guimarães', 41.441500, -8.291800),
    (12, 'Nuno Neves', 'nuno.neves@localhub.test',
     'AQAAAAIAAYagAAAAEPgsjAU8KkAdqtVIyqrpFduH5Zj+guDDc3umY8M1TP12xVDC/ZlTqRZmuo2dJWXk7Q==',
     'PROVIDER', 'INACTIVE', 'Rua de Cedofeita Fictícia, 100, 4050-002 Porto', 41.161000, -8.602000),
    (13, 'Olga Oliveira', 'olga.oliveira@localhub.test',
     'AQAAAAIAAYagAAAAEH3g4iYWxLI53pidEVnwH+mFKkG34DPnOWdwIhB6/DGhilmR3J/mg4Y8eDFiN/Fm5Q==',
     'PROVIDER', 'ACTIVE', NULL, NULL, NULL);                                 -- sem localização nem perfil

-- -----------------------------------------------------------------------------
-- 3. Perfis profissionais e áreas de atuação
--    averageRating = média das avaliações dos serviços do Prestador, com uma
--    casa decimal (ProfessionalProfile.CalculateAverageRating); NULL sem avaliações.
-- -----------------------------------------------------------------------------

INSERT INTO "ProfessionalProfile"
    ("professionalProfileId", "serviceProviderId", "introduction", "contactDetails", "experience",
     "averageRating", "serviceRadiusKm")
OVERRIDING SYSTEM VALUE VALUES
    (1, 8, 'Canalizador e eletricista certificado. Reparações e instalações em habitações.',
     'Telefone 900 000 001 (fictício); hugo.henriques@localhub.test',
     '12 anos em canalização e instalações elétricas residenciais.', 5.0, 25),
    (2, 9, 'Jardinagem e limpeza de casas e espaços exteriores.',
     'Telefone 900 000 002 (fictício); ines.igreja@localhub.test',
     '8 anos em manutenção de jardins e limpezas domésticas.', NULL, 30),
    (3, 10, 'Pintor de interiores e exteriores; pequenas reparações de canalização.',
     'Telefone 900 000 003 (fictício); joao.jardim@localhub.test',
     '15 anos em pintura de habitações.', 2.0, 40),
    (4, 11, 'Limpezas domésticas regulares e pontuais.',
     'Telefone 900 000 004 (fictício); lucia.lopes@localhub.test',
     '5 anos em limpezas domésticas.', 4.0, 15),
    (5, 12, 'Canalizador.',
     'Telefone 900 000 005 (fictício); nuno.neves@localhub.test',
     '3 anos em canalização.', NULL, 20);

INSERT INTO "ServiceArea" ("serviceCategoryId", "professionalProfileId") VALUES
    (1, 1), (2, 1),          -- Hugo: Canalização, Eletricidade
    (3, 2), (4, 2),          -- Inês: Jardinagem, Limpeza doméstica
    (5, 3), (1, 3),          -- João: Pintura, Canalização
    (4, 4),                  -- Lúcia: Limpeza doméstica
    (1, 5);                  -- Nuno: Canalização

-- -----------------------------------------------------------------------------
-- 4. Serviços
-- -----------------------------------------------------------------------------

INSERT INTO "Service"
    ("serviceId", "clientId", "serviceCategoryId", "proposalModel", "title", "description",
     "requestedStartDate", "requestedEndDate", "status", "location", "latitude", "longitude",
     "creationDate", "actualCompletionDate")
OVERRIDING SYSTEM VALUE VALUES
    -- Aberto, público, com duas propostas pendentes (aceitar ou recusar)
    (1, 3, 1, 'PUBLIC', 'Reparar fuga na torneira da cozinha',
     'A torneira da cozinha pinga continuamente e é preciso substituir o vedante ou a torneira.',
     '2027-01-15', '2027-01-15', 'OPEN', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000,
     '2026-10-20 10:00:00+00', NULL),
    -- Aberto, solicitação direta pendente ao Hugo (apresentar proposta ou recusar)
    (2, 3, 2, 'DIRECT_REQUEST', 'Instalar candeeiro na sala',
     'Instalar um candeeiro de teto na sala, com ponto de luz já existente.',
     '2027-01-20', NULL, 'OPEN', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000,
     '2026-10-21 09:30:00+00', NULL),
    -- Atribuído (solicitação direta com proposta aceite)
    (3, 4, 3, 'DIRECT_REQUEST', 'Cortar relva e podar sebes',
     'Corte da relva e poda das sebes de um jardim com cerca de 200 m².',
     '2027-01-10', '2027-01-11', 'ASSIGNED', 'Rua do Mosteiro Fictícia, 5, 4610-001 Felgueiras', 41.364500, -8.198000,
     '2026-10-18 14:00:00+00', NULL),
    -- Realizado e avaliado (5)
    (4, 3, 1, 'PUBLIC', 'Desentupir o lava-loiça',
     'O lava-loiça da cozinha está entupido.',
     '2026-10-12', NULL, 'COMPLETED', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000,
     '2026-10-05 08:00:00+00', '2026-10-12 17:00:00+00'),
    -- Realizado e avaliado (4)
    (5, 5, 4, 'PUBLIC', 'Limpeza geral do apartamento',
     'Limpeza geral de um apartamento T2 antes de uma mudança.',
     '2026-10-14', '2026-10-14', 'COMPLETED', 'Largo do Castelo Fictício, 3, 4800-001 Guimarães', 41.444400, -8.296200,
     '2026-10-06 11:00:00+00', '2026-10-14 18:00:00+00'),
    -- Realizado e ainda não avaliado (criar avaliação)
    (6, 4, 4, 'DIRECT_REQUEST', 'Limpeza semanal da moradia',
     'Limpeza de uma moradia de dois pisos.',
     '2026-10-25', NULL, 'COMPLETED', 'Rua do Mosteiro Fictícia, 5, 4610-001 Felgueiras', 41.364500, -8.198000,
     '2026-10-16 10:00:00+00', '2026-10-25 16:00:00+00'),
    -- Cancelado pelo Cliente no estado aberto
    (7, 5, 5, 'PUBLIC', 'Pintar a fachada',
     'Pintura da fachada de uma moradia de dois pisos.',
     '2026-11-20', '2026-11-25', 'CANCELLED', 'Largo do Castelo Fictício, 3, 4800-001 Guimarães', 41.444400, -8.296200,
     '2026-10-08 15:00:00+00', NULL),
    -- Cancelado pelo Prestador no estado atribuído (conta para os alertas de conduta)
    (8, 3, 2, 'PUBLIC', 'Substituir o quadro elétrico',
     'Substituir o quadro elétrico antigo por um quadro com disjuntores diferenciais.',
     '2026-11-10', NULL, 'CANCELLED', 'Rua das Flores Fictícia, 12, 4050-001 Porto', 41.149600, -8.611000,
     '2026-10-09 09:00:00+00', NULL),
    -- Aberto, solicitação direta recusada pelo Prestador
    (9, 4, 1, 'DIRECT_REQUEST', 'Substituir o autoclismo',
     'Substituir o autoclismo da casa de banho.',
     '2027-01-25', NULL, 'OPEN', 'Rua do Mosteiro Fictícia, 5, 4610-001 Felgueiras', 41.364500, -8.198000,
     '2026-10-22 16:00:00+00', NULL),
    -- Realizado e avaliado (2)
    (10, 5, 5, 'PUBLIC', 'Pintar dois quartos',
     'Pintura das paredes e tetos de dois quartos.',
     '2026-10-10', '2026-10-11', 'COMPLETED', 'Largo do Castelo Fictício, 3, 4800-001 Guimarães', 41.444400, -8.296200,
     '2026-10-02 10:00:00+00', '2026-10-11 18:00:00+00'),
    -- Aberto, de um Cliente com a conta desativada (não aceita propostas)
    (11, 7, 3, 'PUBLIC', 'Limpar o jardim',
     'Limpeza de folhas e ervas daninhas de um jardim pequeno.',
     '2027-02-01', NULL, 'OPEN', 'Avenida da Sé Fictícia, 40, 4700-001 Braga', 41.545400, -8.426500,
     '2026-10-15 12:00:00+00', NULL);

-- -----------------------------------------------------------------------------
-- 5. Solicitações diretas
-- -----------------------------------------------------------------------------

INSERT INTO "DirectRequest" ("directRequestId", "serviceId", "professionalProfileId", "status", "responseDate", "creationDate")
OVERRIDING SYSTEM VALUE VALUES
    (1, 2, 1, 'PENDING',            NULL,                     '2026-10-21 09:30:00+00'),
    (2, 3, 2, 'PROPOSAL_SUBMITTED', '2026-10-19 10:00:00+00', '2026-10-18 14:00:00+00'),
    (3, 6, 2, 'PROPOSAL_SUBMITTED', '2026-10-17 09:00:00+00', '2026-10-16 10:00:00+00'),
    (4, 9, 3, 'DECLINED',           '2026-10-23 08:00:00+00', '2026-10-22 16:00:00+00');

-- -----------------------------------------------------------------------------
-- 6. Propostas
--    Na aceitação, as outras propostas pendentes ficam recusadas (com data de
--    decisão); no cancelamento em aberto, ficam anuladas (sem data de decisão).
-- -----------------------------------------------------------------------------

INSERT INTO "Proposal"
    ("proposalId", "serviceId", "professionalProfileId", "conditions", "obs", "amount", "status",
     "decisionDate", "creationDate")
OVERRIDING SYSTEM VALUE VALUES
    (1, 1, 1, 'Substituição do vedante; material incluído.', NULL, 85.00, 'PENDING',
     NULL, '2026-10-20 15:00:00+00'),
    (2, 1, 3, 'Substituição da torneira; torneira paga à parte.', 'Disponível ao fim de semana.', 95.00, 'PENDING',
     NULL, '2026-10-21 11:00:00+00'),
    (3, 3, 2, 'Corte e poda; resíduos levados pelo Prestador.', NULL, 120.00, 'ACCEPTED',
     '2026-10-20 09:00:00+00', '2026-10-19 10:00:00+00'),
    (4, 4, 1, 'Desentupimento com máquina; deslocação incluída.', NULL, 60.00, 'ACCEPTED',
     '2026-10-07 10:00:00+00', '2026-10-05 16:00:00+00'),
    (5, 4, 3, 'Desentupimento manual.', NULL, 75.00, 'DECLINED',
     '2026-10-07 10:00:00+00', '2026-10-06 09:00:00+00'),
    (6, 5, 4, 'Limpeza geral, 6 horas; produtos incluídos.', NULL, 45.00, 'ACCEPTED',
     '2026-10-08 12:00:00+00', '2026-10-06 18:00:00+00'),
    (7, 5, 2, 'Limpeza geral, 5 horas.', NULL, 50.00, 'DECLINED',
     '2026-10-08 12:00:00+00', '2026-10-07 10:00:00+00'),
    (8, 6, 2, 'Limpeza da moradia, 4 horas.', NULL, 70.00, 'ACCEPTED',
     '2026-10-18 10:00:00+00', '2026-10-17 09:00:00+00'),
    (9, 7, 3, 'Pintura da fachada com tinta acrílica; andaime incluído.', NULL, 300.00, 'VOID',
     NULL, '2026-10-09 10:00:00+00'),
    (10, 8, 1, 'Substituição do quadro; material incluído.', NULL, 150.00, 'ACCEPTED',
     '2026-10-10 09:00:00+00', '2026-10-09 15:00:00+00'),
    (11, 10, 3, 'Pintura de paredes e tetos; tinta incluída.', NULL, 400.00, 'ACCEPTED',
     '2026-10-04 10:00:00+00', '2026-10-03 09:00:00+00');

-- -----------------------------------------------------------------------------
-- 7. Avaliações (só de serviços realizados, no máximo uma por serviço)
-- -----------------------------------------------------------------------------

INSERT INTO "Review" ("reviewId", "serviceId", "rating", "comment", "creationDate")
OVERRIDING SYSTEM VALUE VALUES
    (1, 4,  5, 'Rápido e eficaz.',                      '2026-10-13 09:00:00+00'),
    (2, 5,  4, 'Bom trabalho; atrasou-se um pouco.',    '2026-10-15 10:00:00+00'),
    (3, 10, 2, 'Trabalho incompleto num dos quartos.',  '2026-10-12 11:00:00+00');

-- -----------------------------------------------------------------------------
-- 8. Histórico de estados dos serviços
--    A criação do serviço não é registada como mudança de estado.
-- -----------------------------------------------------------------------------

INSERT INTO "ServiceStatusHistory"
    ("serviceStatusHistoryId", "serviceId", "previousStatus", "newStatus", "changedAt", "changedByUserId")
OVERRIDING SYSTEM VALUE VALUES
    (1,  3,  'OPEN',     'ASSIGNED',  '2026-10-20 09:00:00+00', 4),
    (2,  4,  'OPEN',     'ASSIGNED',  '2026-10-07 10:00:00+00', 3),
    (3,  4,  'ASSIGNED', 'COMPLETED', '2026-10-12 17:00:00+00', 3),
    (4,  5,  'OPEN',     'ASSIGNED',  '2026-10-08 12:00:00+00', 5),
    (5,  5,  'ASSIGNED', 'COMPLETED', '2026-10-14 18:00:00+00', 5),
    (6,  6,  'OPEN',     'ASSIGNED',  '2026-10-18 10:00:00+00', 4),
    (7,  6,  'ASSIGNED', 'COMPLETED', '2026-10-25 16:00:00+00', 4),
    (8,  7,  'OPEN',     'CANCELLED', '2026-10-10 08:00:00+00', 5),
    (9,  8,  'OPEN',     'ASSIGNED',  '2026-10-10 09:00:00+00', 3),
    (10, 8,  'ASSIGNED', 'CANCELLED', '2026-10-11 08:00:00+00', 8),
    (11, 10, 'OPEN',     'ASSIGNED',  '2026-10-04 10:00:00+00', 5),
    (12, 10, 'ASSIGNED', 'COMPLETED', '2026-10-11 18:00:00+00', 5);

-- -----------------------------------------------------------------------------
-- 9. Sequências das colunas de identidade: o próximo registo criado pela API
--    recebe o identificador seguinte ao maior do conjunto de referência.
-- -----------------------------------------------------------------------------

DO $$
BEGIN
    PERFORM setval(pg_get_serial_sequence('"ServiceCategory"', 'serviceCategoryId'), 6);
    PERFORM setval(pg_get_serial_sequence('"User"', 'userId'), 13);
    PERFORM setval(pg_get_serial_sequence('"ProfessionalProfile"', 'professionalProfileId'), 5);
    PERFORM setval(pg_get_serial_sequence('"Service"', 'serviceId'), 11);
    PERFORM setval(pg_get_serial_sequence('"DirectRequest"', 'directRequestId'), 4);
    PERFORM setval(pg_get_serial_sequence('"Proposal"', 'proposalId'), 11);
    PERFORM setval(pg_get_serial_sequence('"Review"', 'reviewId'), 3);
    PERFORM setval(pg_get_serial_sequence('"ServiceStatusHistory"', 'serviceStatusHistoryId'), 12);
END $$;

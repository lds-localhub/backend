-- =============================================================================
-- LocalHub — Esquema da base de dados (PostgreSQL 18)
-- Issue: I038 — Elaborar os scripts das bases de dados v1
-- Fonte: m2-modelo-dados-v01.md (secção 5 — Modelo físico e dicionário de dados)
--
-- Os nomes das tabelas, das colunas e dos tipos enumerados são os do Modelo de
-- Dados v01. As tabelas e as colunas usam identificadores entre aspas para manter
-- as maiúsculas e minúsculas do modelo (por exemplo, "User"."userId").
--
-- Ficam na base de dados as restrições que dependem só da própria linha (CHECK),
-- as chaves e a unicidade. As regras que dependem de outras tabelas ou do momento
-- da operação (por exemplo, «utilizador com o papel PROVIDER» ou «categoria ativa
-- no momento da criação») são validadas no backend (Documentação da Arquitetura v02).
--
-- Este ficheiro é incluído por criar-base-dados.sql; não é executado sozinho.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Tipos enumerados (Modelo de Dados v01, 5.1.1)
-- -----------------------------------------------------------------------------

CREATE TYPE user_role AS ENUM ('CLIENT', 'PROVIDER', 'ADMIN');

CREATE TYPE activation_status AS ENUM ('ACTIVE', 'INACTIVE');

CREATE TYPE proposal_model AS ENUM ('PUBLIC', 'DIRECT_REQUEST');

CREATE TYPE service_status AS ENUM ('OPEN', 'ASSIGNED', 'COMPLETED', 'CANCELLED');

CREATE TYPE direct_request_status AS ENUM ('PENDING', 'PROPOSAL_SUBMITTED', 'DECLINED', 'VOID');

CREATE TYPE proposal_status AS ENUM ('PENDING', 'ACCEPTED', 'DECLINED', 'VOID');

CREATE TYPE notification_event_type AS ENUM (
    'DIRECT_REQUEST_RECEIVED',
    'MATCHING_PUBLIC_SERVICE',
    'PROPOSAL_RECEIVED',
    'DIRECT_REQUEST_DECLINED',
    'SERVICE_CANCELLED_BY_PROVIDER',
    'PROPOSAL_ACCEPTED',
    'SERVICE_CANCELLED_BY_CLIENT'
);

-- -----------------------------------------------------------------------------
-- 2. Tabelas
-- -----------------------------------------------------------------------------

-- User — conta de Cliente, Prestador de Serviços ou Administrador
CREATE TABLE "User" (
    "userId"       integer GENERATED ALWAYS AS IDENTITY,
    "name"         varchar(100)      NOT NULL,
    "email"        varchar(254)      NOT NULL,
    "passwordHash" varchar(255)      NOT NULL,
    "role"         user_role         NOT NULL,
    "status"       activation_status NOT NULL DEFAULT 'ACTIVE',
    "address"      varchar(255),
    "latitude"     numeric(8,6),
    "longitude"    numeric(9,6),
    CONSTRAINT "PK_User" PRIMARY KEY ("userId"),
    CONSTRAINT "CK_User_name" CHECK (btrim("name") <> ''),
    CONSTRAINT "CK_User_email" CHECK ("email" ~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'),
    CONSTRAINT "CK_User_passwordHash" CHECK (btrim("passwordHash") <> ''),
    CONSTRAINT "CK_User_latitude" CHECK ("latitude" BETWEEN -90 AND 90),
    CONSTRAINT "CK_User_longitude" CHECK ("longitude" BETWEEN -180 AND 180),
    -- latitude e longitude são definidas em conjunto
    CONSTRAINT "CK_User_coordinates" CHECK (("latitude" IS NULL) = ("longitude" IS NULL)),
    -- morada não vazia quando está definida uma localização
    CONSTRAINT "CK_User_address" CHECK (
        "latitude" IS NULL OR ("address" IS NOT NULL AND btrim("address") <> '')
    )
);

-- e-mail único sem distinguir maiúsculas de minúsculas
CREATE UNIQUE INDEX "UX_User_email" ON "User" (lower("email"));

-- UserSession — sessão de um utilizador (validade máxima de 8 horas)
CREATE TABLE "UserSession" (
    "sessionId" integer GENERATED ALWAYS AS IDENTITY,
    "userId"    integer     NOT NULL,
    "createdAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "expiresAt" timestamptz NOT NULL,
    "revokedAt" timestamptz,
    CONSTRAINT "PK_UserSession" PRIMARY KEY ("sessionId"),
    CONSTRAINT "FK_UserSession_User" FOREIGN KEY ("userId")
        REFERENCES "User" ("userId") ON DELETE CASCADE,
    CONSTRAINT "CK_UserSession_expiresAt" CHECK (
        "expiresAt" > "createdAt" AND "expiresAt" <= "createdAt" + interval '8 hours'
    ),
    CONSTRAINT "CK_UserSession_revokedAt" CHECK ("revokedAt" IS NULL OR "revokedAt" >= "createdAt")
);

CREATE INDEX "IX_UserSession_userId" ON "UserSession" ("userId");

-- PushRegistration — instalação da aplicação mobile registada para notificações push
CREATE TABLE "PushRegistration" (
    "pushRegistrationId" integer GENERATED ALWAYS AS IDENTITY,
    "userId"             integer     NOT NULL,
    "pushToken"          text        NOT NULL,
    "updatedAt"          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_PushRegistration" PRIMARY KEY ("pushRegistrationId"),
    CONSTRAINT "FK_PushRegistration_User" FOREIGN KEY ("userId")
        REFERENCES "User" ("userId") ON DELETE CASCADE,
    CONSTRAINT "UQ_PushRegistration_pushToken" UNIQUE ("pushToken"),
    CONSTRAINT "CK_PushRegistration_pushToken" CHECK (btrim("pushToken") <> '')
);

CREATE INDEX "IX_PushRegistration_userId" ON "PushRegistration" ("userId");

-- ServiceCategory — categoria de serviços ao domicílio
CREATE TABLE "ServiceCategory" (
    "serviceCategoryId" integer GENERATED ALWAYS AS IDENTITY,
    "name"              varchar(60)       NOT NULL,
    "status"            activation_status NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT "PK_ServiceCategory" PRIMARY KEY ("serviceCategoryId"),
    CONSTRAINT "CK_ServiceCategory_name" CHECK (btrim("name") <> '')
);

-- nome único sem distinguir maiúsculas de minúsculas
CREATE UNIQUE INDEX "UX_ServiceCategory_name" ON "ServiceCategory" (lower("name"));

-- ProfessionalProfile — perfil profissional de um Prestador de Serviços
CREATE TABLE "ProfessionalProfile" (
    "professionalProfileId" integer GENERATED ALWAYS AS IDENTITY,
    "serviceProviderId"     integer       NOT NULL,
    "introduction"          varchar(1000) NOT NULL,
    "contactDetails"        varchar(500)  NOT NULL,
    "experience"            varchar(2000) NOT NULL,
    "averageRating"         numeric(2,1),
    "serviceRadiusKm"       smallint      NOT NULL,
    CONSTRAINT "PK_ProfessionalProfile" PRIMARY KEY ("professionalProfileId"),
    CONSTRAINT "FK_ProfessionalProfile_User" FOREIGN KEY ("serviceProviderId")
        REFERENCES "User" ("userId") ON DELETE RESTRICT,
    -- no máximo um perfil profissional por Prestador
    CONSTRAINT "UQ_ProfessionalProfile_serviceProviderId" UNIQUE ("serviceProviderId"),
    CONSTRAINT "CK_ProfessionalProfile_introduction" CHECK (btrim("introduction") <> ''),
    CONSTRAINT "CK_ProfessionalProfile_contactDetails" CHECK (btrim("contactDetails") <> ''),
    CONSTRAINT "CK_ProfessionalProfile_experience" CHECK (btrim("experience") <> ''),
    -- atributo derivado das avaliações; nulo quando o Prestador não tem avaliações
    CONSTRAINT "CK_ProfessionalProfile_averageRating" CHECK ("averageRating" BETWEEN 1.0 AND 5.0),
    CONSTRAINT "CK_ProfessionalProfile_serviceRadiusKm" CHECK ("serviceRadiusKm" BETWEEN 1 AND 100)
);

-- ServiceArea — associação entre um perfil profissional e uma categoria de serviço
CREATE TABLE "ServiceArea" (
    "serviceCategoryId"     integer NOT NULL,
    "professionalProfileId" integer NOT NULL,
    CONSTRAINT "PK_ServiceArea" PRIMARY KEY ("serviceCategoryId", "professionalProfileId"),
    CONSTRAINT "FK_ServiceArea_ServiceCategory" FOREIGN KEY ("serviceCategoryId")
        REFERENCES "ServiceCategory" ("serviceCategoryId") ON DELETE RESTRICT,
    CONSTRAINT "FK_ServiceArea_ProfessionalProfile" FOREIGN KEY ("professionalProfileId")
        REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE CASCADE
);

CREATE INDEX "IX_ServiceArea_professionalProfileId" ON "ServiceArea" ("professionalProfileId");

-- Service — serviço criado por um Cliente (aberto a propostas ou com solicitações diretas)
CREATE TABLE "Service" (
    "serviceId"            integer GENERATED ALWAYS AS IDENTITY,
    "clientId"             integer        NOT NULL,
    "serviceCategoryId"    integer        NOT NULL,
    "proposalModel"        proposal_model NOT NULL,
    "description"          varchar(1000)  NOT NULL,
    "requestedStartDate"   date           NOT NULL,
    "requestedEndDate"     date,
    "status"               service_status NOT NULL DEFAULT 'OPEN',
    "location"             varchar(255)   NOT NULL,
    "latitude"             numeric(8,6)   NOT NULL,
    "longitude"            numeric(9,6)   NOT NULL,
    "creationDate"         timestamptz    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualCompletionDate" timestamptz,
    CONSTRAINT "PK_Service" PRIMARY KEY ("serviceId"),
    CONSTRAINT "FK_Service_User" FOREIGN KEY ("clientId")
        REFERENCES "User" ("userId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Service_ServiceCategory" FOREIGN KEY ("serviceCategoryId")
        REFERENCES "ServiceCategory" ("serviceCategoryId") ON DELETE RESTRICT,
    CONSTRAINT "CK_Service_description" CHECK (btrim("description") <> ''),
    -- data pretendida igual ou posterior à data de criação (data civil em Portugal)
    CONSTRAINT "CK_Service_requestedStartDate" CHECK (
        "requestedStartDate" >= ("creationDate" AT TIME ZONE 'Europe/Lisbon')::date
    ),
    CONSTRAINT "CK_Service_requestedEndDate" CHECK (
        "requestedEndDate" IS NULL OR "requestedEndDate" >= "requestedStartDate"
    ),
    CONSTRAINT "CK_Service_location" CHECK (btrim("location") <> ''),
    CONSTRAINT "CK_Service_latitude" CHECK ("latitude" BETWEEN -90 AND 90),
    CONSTRAINT "CK_Service_longitude" CHECK ("longitude" BETWEEN -180 AND 180),
    -- data de realização obrigatória só no estado COMPLETED e nula nos restantes
    CONSTRAINT "CK_Service_actualCompletionDate" CHECK (
        ("status" = 'COMPLETED') = ("actualCompletionDate" IS NOT NULL)
        AND ("actualCompletionDate" IS NULL OR "actualCompletionDate" >= "creationDate")
    )
);

CREATE INDEX "IX_Service_clientId" ON "Service" ("clientId");
CREATE INDEX "IX_Service_serviceCategoryId_status" ON "Service" ("serviceCategoryId", "status");

-- ServiceStatusHistory — registo de cada alteração de estado de um serviço
CREATE TABLE "ServiceStatusHistory" (
    "serviceStatusHistoryId" integer GENERATED ALWAYS AS IDENTITY,
    "serviceId"              integer        NOT NULL,
    "previousStatus"         service_status NOT NULL,
    "newStatus"              service_status NOT NULL,
    "changedAt"              timestamptz    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "changedByUserId"        integer        NOT NULL,
    CONSTRAINT "PK_ServiceStatusHistory" PRIMARY KEY ("serviceStatusHistoryId"),
    CONSTRAINT "FK_ServiceStatusHistory_Service" FOREIGN KEY ("serviceId")
        REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
    CONSTRAINT "FK_ServiceStatusHistory_User" FOREIGN KEY ("changedByUserId")
        REFERENCES "User" ("userId") ON DELETE RESTRICT,
    -- transições válidas: OPEN -> ASSIGNED | CANCELLED; ASSIGNED -> COMPLETED | CANCELLED
    CONSTRAINT "CK_ServiceStatusHistory_transition" CHECK (
        ("previousStatus" = 'OPEN' AND "newStatus" IN ('ASSIGNED', 'CANCELLED'))
        OR ("previousStatus" = 'ASSIGNED' AND "newStatus" IN ('COMPLETED', 'CANCELLED'))
    )
);

CREATE INDEX "IX_ServiceStatusHistory_serviceId" ON "ServiceStatusHistory" ("serviceId");
CREATE INDEX "IX_ServiceStatusHistory_changedByUserId" ON "ServiceStatusHistory" ("changedByUserId");

-- DirectRequest — solicitação direta de proposta a um Prestador selecionado
CREATE TABLE "DirectRequest" (
    "directRequestId"       integer GENERATED ALWAYS AS IDENTITY,
    "serviceId"             integer               NOT NULL,
    "professionalProfileId" integer               NOT NULL,
    "status"                direct_request_status NOT NULL DEFAULT 'PENDING',
    "responseDate"          timestamptz,
    "creationDate"          timestamptz           NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_DirectRequest" PRIMARY KEY ("directRequestId"),
    CONSTRAINT "FK_DirectRequest_Service" FOREIGN KEY ("serviceId")
        REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
    CONSTRAINT "FK_DirectRequest_ProfessionalProfile" FOREIGN KEY ("professionalProfileId")
        REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE RESTRICT,
    -- uma única solicitação por perfil e serviço
    CONSTRAINT "UQ_DirectRequest_professionalProfileId_serviceId"
        UNIQUE ("professionalProfileId", "serviceId"),
    -- data de resposta obrigatória para PROPOSAL_SUBMITTED e DECLINED; nula para PENDING e VOID
    CONSTRAINT "CK_DirectRequest_responseDate" CHECK (
        ("status" IN ('PROPOSAL_SUBMITTED', 'DECLINED')) = ("responseDate" IS NOT NULL)
        AND ("responseDate" IS NULL OR "responseDate" >= "creationDate")
    )
);

CREATE INDEX "IX_DirectRequest_serviceId" ON "DirectRequest" ("serviceId");

-- Proposal — proposta de um Prestador para um serviço
CREATE TABLE "Proposal" (
    "proposalId"            integer GENERATED ALWAYS AS IDENTITY,
    "serviceId"             integer         NOT NULL,
    "professionalProfileId" integer         NOT NULL,
    "conditions"            varchar(1000)   NOT NULL,
    "obs"                   varchar(500),
    "amount"                numeric(8,2)    NOT NULL,
    "status"                proposal_status NOT NULL DEFAULT 'PENDING',
    "decisionDate"          timestamptz,
    "creationDate"          timestamptz     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_Proposal" PRIMARY KEY ("proposalId"),
    CONSTRAINT "FK_Proposal_Service" FOREIGN KEY ("serviceId")
        REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
    CONSTRAINT "FK_Proposal_ProfessionalProfile" FOREIGN KEY ("professionalProfileId")
        REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE RESTRICT,
    -- uma única proposta por perfil e serviço
    CONSTRAINT "UQ_Proposal_professionalProfileId_serviceId"
        UNIQUE ("professionalProfileId", "serviceId"),
    CONSTRAINT "CK_Proposal_conditions" CHECK (btrim("conditions") <> ''),
    CONSTRAINT "CK_Proposal_obs" CHECK ("obs" IS NULL OR btrim("obs") <> ''),
    CONSTRAINT "CK_Proposal_amount" CHECK ("amount" > 0 AND "amount" <= 100000),
    -- data de decisão nula em PENDING e obrigatória em ACCEPTED, DECLINED e VOID
    CONSTRAINT "CK_Proposal_decisionDate" CHECK (
        ("status" = 'PENDING') = ("decisionDate" IS NULL)
        AND ("decisionDate" IS NULL OR "decisionDate" >= "creationDate")
    )
);

CREATE INDEX "IX_Proposal_serviceId" ON "Proposal" ("serviceId");
-- no máximo uma proposta aceite por serviço
CREATE UNIQUE INDEX "UX_Proposal_serviceId_accepted" ON "Proposal" ("serviceId")
    WHERE "status" = 'ACCEPTED';

-- Review — avaliação do Prestador pelo Cliente após a realização do serviço
CREATE TABLE "Review" (
    "reviewId"     integer GENERATED ALWAYS AS IDENTITY,
    "serviceId"    integer      NOT NULL,
    "rating"       smallint     NOT NULL,
    "comment"      varchar(500),
    "creationDate" timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_Review" PRIMARY KEY ("reviewId"),
    CONSTRAINT "FK_Review_Service" FOREIGN KEY ("serviceId")
        REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
    -- no máximo uma avaliação por serviço
    CONSTRAINT "UQ_Review_serviceId" UNIQUE ("serviceId"),
    CONSTRAINT "CK_Review_rating" CHECK ("rating" BETWEEN 1 AND 5),
    CONSTRAINT "CK_Review_comment" CHECK ("comment" IS NULL OR btrim("comment") <> '')
);

-- Notification — notificação gerada por um evento de um serviço
CREATE TABLE "Notification" (
    "notificationId" integer GENERATED ALWAYS AS IDENTITY,
    "serviceId"      integer                 NOT NULL,
    "eventType"      notification_event_type NOT NULL,
    "message"        varchar(500)            NOT NULL,
    "creationDate"   timestamptz             NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_Notification" PRIMARY KEY ("notificationId"),
    CONSTRAINT "FK_Notification_Service" FOREIGN KEY ("serviceId")
        REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
    CONSTRAINT "CK_Notification_message" CHECK (btrim("message") <> '')
);

CREATE INDEX "IX_Notification_serviceId" ON "Notification" ("serviceId");

-- NotificationRecipient — destinatário de uma notificação e estado de leitura individual
CREATE TABLE "NotificationRecipient" (
    "userId"         integer     NOT NULL,
    "notificationId" integer     NOT NULL,
    "readStatus"     boolean     NOT NULL DEFAULT false,
    "deletedAt"      timestamptz,
    CONSTRAINT "PK_NotificationRecipient" PRIMARY KEY ("userId", "notificationId"),
    CONSTRAINT "FK_NotificationRecipient_User" FOREIGN KEY ("userId")
        REFERENCES "User" ("userId") ON DELETE CASCADE,
    CONSTRAINT "FK_NotificationRecipient_Notification" FOREIGN KEY ("notificationId")
        REFERENCES "Notification" ("notificationId") ON DELETE CASCADE
);

CREATE INDEX "IX_NotificationRecipient_notificationId" ON "NotificationRecipient" ("notificationId");

-- -----------------------------------------------------------------------------
-- 3. Descrição das tabelas
-- -----------------------------------------------------------------------------

COMMENT ON TABLE "User" IS 'Conta de Cliente, Prestador de Serviços ou Administrador.';
COMMENT ON TABLE "UserSession" IS 'Sessão de um utilizador, com validade máxima de 8 horas.';
COMMENT ON TABLE "PushRegistration" IS 'Instalação da aplicação mobile registada para notificações push.';
COMMENT ON TABLE "ServiceCategory" IS 'Categoria de serviços ao domicílio.';
COMMENT ON TABLE "ProfessionalProfile" IS 'Perfil profissional de um Prestador de Serviços.';
COMMENT ON TABLE "ServiceArea" IS 'Categoria de serviço em que um perfil profissional atua.';
COMMENT ON TABLE "Service" IS 'Serviço criado por um Cliente, aberto a propostas ou com solicitações diretas.';
COMMENT ON TABLE "ServiceStatusHistory" IS 'Registo de cada alteração de estado de um serviço.';
COMMENT ON TABLE "DirectRequest" IS 'Solicitação direta de proposta a um Prestador selecionado.';
COMMENT ON TABLE "Proposal" IS 'Proposta de um Prestador para um serviço.';
COMMENT ON TABLE "Review" IS 'Avaliação do Prestador pelo Cliente após a realização do serviço.';
COMMENT ON TABLE "Notification" IS 'Notificação gerada por um evento de um serviço.';
COMMENT ON TABLE "NotificationRecipient" IS 'Destinatário de uma notificação e respetivo estado de leitura.';
COMMENT ON COLUMN "User"."passwordHash" IS 'Resumo criptográfico com sal (ASP.NET Core Identity PasswordHasher V3); nunca a palavra-passe legível (RNF-01).';
COMMENT ON COLUMN "ProfessionalProfile"."averageRating" IS 'Atributo derivado: média das classificações recebidas, mantida pelo backend.';

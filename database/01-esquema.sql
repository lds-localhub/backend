CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "migrationId" character varying(150) NOT NULL,
    "productVersion" character varying(32) NOT NULL,
    CONSTRAINT "pK___EFMigrationsHistory" PRIMARY KEY ("migrationId")
);


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TYPE activation_status AS ENUM ('ACTIVE', 'INACTIVE');
    CREATE TYPE direct_request_status AS ENUM ('DECLINED', 'PENDING', 'PROPOSAL_SUBMITTED', 'VOID');
    CREATE TYPE proposal_model AS ENUM ('DIRECT_REQUEST', 'PUBLIC');
    CREATE TYPE proposal_status AS ENUM ('ACCEPTED', 'DECLINED', 'PENDING', 'VOID');
    CREATE TYPE service_status AS ENUM ('ASSIGNED', 'CANCELLED', 'COMPLETED', 'OPEN');
    CREATE TYPE user_role AS ENUM ('ADMIN', 'CLIENT', 'PROVIDER');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "ServiceCategory" (
        "serviceCategoryId" integer GENERATED ALWAYS AS IDENTITY,
        name character varying(60) NOT NULL,
        status activation_status NOT NULL,
        CONSTRAINT "pK_ServiceCategory" PRIMARY KEY ("serviceCategoryId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "User" (
        "userId" integer GENERATED ALWAYS AS IDENTITY,
        name character varying(100) NOT NULL,
        email character varying(254) NOT NULL,
        "passwordHash" character varying(255) NOT NULL,
        role user_role NOT NULL,
        status activation_status NOT NULL,
        address character varying(255),
        latitude numeric(8,6),
        longitude numeric(9,6),
        CONSTRAINT "pK_User" PRIMARY KEY ("userId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "ProfessionalProfile" (
        "professionalProfileId" integer GENERATED ALWAYS AS IDENTITY,
        "serviceProviderId" integer NOT NULL,
        introduction character varying(1000) NOT NULL,
        "contactDetails" character varying(500) NOT NULL,
        experience character varying(2000) NOT NULL,
        "averageRating" numeric(2,1),
        "serviceRadiusKm" smallint NOT NULL,
        CONSTRAINT "pK_ProfessionalProfile" PRIMARY KEY ("professionalProfileId"),
        CONSTRAINT "fK_ProfessionalProfile_users_serviceProviderId" FOREIGN KEY ("serviceProviderId") REFERENCES "User" ("userId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "Service" (
        "serviceId" integer GENERATED ALWAYS AS IDENTITY,
        "clientId" integer NOT NULL,
        "serviceCategoryId" integer NOT NULL,
        "proposalModel" proposal_model NOT NULL,
        title character varying(100) NOT NULL,
        description character varying(1000) NOT NULL,
        "requestedStartDate" date NOT NULL,
        "requestedEndDate" date,
        status service_status NOT NULL,
        location character varying(255) NOT NULL,
        latitude numeric(8,6) NOT NULL,
        longitude numeric(9,6) NOT NULL,
        "creationDate" timestamp with time zone NOT NULL,
        "actualCompletionDate" timestamp with time zone,
        CONSTRAINT "pK_Service" PRIMARY KEY ("serviceId"),
        CONSTRAINT "fK_Service_ServiceCategory_serviceCategoryId" FOREIGN KEY ("serviceCategoryId") REFERENCES "ServiceCategory" ("serviceCategoryId") ON DELETE RESTRICT,
        CONSTRAINT "fK_Service_users_clientId" FOREIGN KEY ("clientId") REFERENCES "User" ("userId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "UserSession" (
        "sessionId" integer GENERATED ALWAYS AS IDENTITY,
        "userId" integer NOT NULL,
        "createdAt" timestamp with time zone NOT NULL,
        "expiresAt" timestamp with time zone NOT NULL,
        "revokedAt" timestamp with time zone,
        CONSTRAINT "pK_UserSession" PRIMARY KEY ("sessionId"),
        CONSTRAINT "fK_UserSession_User_userId" FOREIGN KEY ("userId") REFERENCES "User" ("userId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "ServiceArea" (
        "serviceCategoryId" integer NOT NULL,
        "professionalProfileId" integer NOT NULL,
        CONSTRAINT "pK_ServiceArea" PRIMARY KEY ("serviceCategoryId", "professionalProfileId"),
        CONSTRAINT "fK_ServiceArea_ProfessionalProfile_professionalProfileId" FOREIGN KEY ("professionalProfileId") REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE CASCADE,
        CONSTRAINT "fK_ServiceArea_serviceCategories_serviceCategoryId" FOREIGN KEY ("serviceCategoryId") REFERENCES "ServiceCategory" ("serviceCategoryId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "DirectRequest" (
        "directRequestId" integer GENERATED ALWAYS AS IDENTITY,
        "serviceId" integer NOT NULL,
        "professionalProfileId" integer NOT NULL,
        status direct_request_status NOT NULL,
        "responseDate" timestamp with time zone,
        "creationDate" timestamp with time zone NOT NULL,
        CONSTRAINT "pK_DirectRequest" PRIMARY KEY ("directRequestId"),
        CONSTRAINT "fK_DirectRequest_professionalProfiles_professionalProfileId" FOREIGN KEY ("professionalProfileId") REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE RESTRICT,
        CONSTRAINT "fK_DirectRequest_services_serviceId" FOREIGN KEY ("serviceId") REFERENCES "Service" ("serviceId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "Proposal" (
        "proposalId" integer GENERATED ALWAYS AS IDENTITY,
        "serviceId" integer NOT NULL,
        "professionalProfileId" integer NOT NULL,
        conditions character varying(1000) NOT NULL,
        obs character varying(500),
        amount numeric(8,2) NOT NULL,
        status proposal_status NOT NULL,
        "decisionDate" timestamp with time zone,
        "creationDate" timestamp with time zone NOT NULL,
        CONSTRAINT "pK_Proposal" PRIMARY KEY ("proposalId"),
        CONSTRAINT "fK_Proposal_ProfessionalProfile_professionalProfileId" FOREIGN KEY ("professionalProfileId") REFERENCES "ProfessionalProfile" ("professionalProfileId") ON DELETE RESTRICT,
        CONSTRAINT "fK_Proposal_services_serviceId" FOREIGN KEY ("serviceId") REFERENCES "Service" ("serviceId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "Review" (
        "reviewId" integer GENERATED ALWAYS AS IDENTITY,
        "serviceId" integer NOT NULL,
        rating smallint NOT NULL,
        comment character varying(500),
        "creationDate" timestamp with time zone NOT NULL,
        CONSTRAINT "pK_Review" PRIMARY KEY ("reviewId"),
        CONSTRAINT "fK_Review_services_serviceId" FOREIGN KEY ("serviceId") REFERENCES "Service" ("serviceId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE TABLE "ServiceStatusHistory" (
        "serviceStatusHistoryId" integer GENERATED ALWAYS AS IDENTITY,
        "serviceId" integer NOT NULL,
        "previousStatus" service_status NOT NULL,
        "newStatus" service_status NOT NULL,
        "changedAt" timestamp with time zone NOT NULL,
        "changedByUserId" integer NOT NULL,
        CONSTRAINT "pK_ServiceStatusHistory" PRIMARY KEY ("serviceStatusHistoryId"),
        CONSTRAINT "fK_ServiceStatusHistory_Service_serviceId" FOREIGN KEY ("serviceId") REFERENCES "Service" ("serviceId") ON DELETE CASCADE,
        CONSTRAINT "fK_ServiceStatusHistory_users_changedByUserId" FOREIGN KEY ("changedByUserId") REFERENCES "User" ("userId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "iX_DirectRequest_professionalProfileId_serviceId" ON "DirectRequest" ("professionalProfileId", "serviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_DirectRequest_serviceId" ON "DirectRequest" ("serviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "iX_ProfessionalProfile_serviceProviderId" ON "ProfessionalProfile" ("serviceProviderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "iX_Proposal_professionalProfileId_serviceId" ON "Proposal" ("professionalProfileId", "serviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Proposal_serviceId_accepted" ON "Proposal" ("serviceId") WHERE "status" = 'ACCEPTED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "iX_Review_serviceId" ON "Review" ("serviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_Service_clientId" ON "Service" ("clientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_Service_serviceCategoryId" ON "Service" ("serviceCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_ServiceArea_professionalProfileId" ON "ServiceArea" ("professionalProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_ServiceCategory_name" ON "ServiceCategory" (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_ServiceStatusHistory_changedByUserId" ON "ServiceStatusHistory" ("changedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_ServiceStatusHistory_serviceId" ON "ServiceStatusHistory" ("serviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_User_email" ON "User" (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    CREATE INDEX "iX_UserSession_userId" ON "UserSession" ("userId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migrationId" = '20261027121052_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("migrationId", "productVersion")
    VALUES ('20261027121052_InitialCreate', '10.0.13');
    END IF;
END $EF$;

-- Inicialización de extensiones y esquema para Open Harness (PostgreSQL 17)
CREATE EXTENSION IF NOT EXISTS "vector";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

CREATE SCHEMA IF NOT EXISTS harness;

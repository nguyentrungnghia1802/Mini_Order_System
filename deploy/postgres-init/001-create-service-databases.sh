#!/usr/bin/env bash
set -Eeuo pipefail

create_role() {
  local role_name="$1"
  local role_password="$2"

  psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set=role_name="$role_name" \
    --set=role_password="$role_password" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'role_name', :'role_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'role_name');
\gexec
SQL
}

create_database() {
  local database_name="$1"
  local owner_name="$2"

  psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set=database_name="$database_name" \
    --set=owner_name="$owner_name" <<'SQL'
SELECT format('CREATE DATABASE %I OWNER %I', :'database_name', :'owner_name')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'database_name');
\gexec
SQL

  psql --username "$POSTGRES_USER" --dbname "$database_name" \
    --set=owner_name="$owner_name" <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO :"owner_name";
SQL

  psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set=database_name="$database_name" \
    --set=owner_name="$owner_name" <<'SQL'
REVOKE CONNECT ON DATABASE :"database_name" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"database_name" TO :"owner_name";
SQL
}

create_role "$PRODUCT_DB_USER" "$PRODUCT_DB_PASSWORD"
create_database "$PRODUCT_DB_NAME" "$PRODUCT_DB_USER"

create_role "$ORDER_DB_USER" "$ORDER_DB_PASSWORD"
create_database "$ORDER_DB_NAME" "$ORDER_DB_USER"

create_role "$NOTIFICATION_DB_USER" "$NOTIFICATION_DB_PASSWORD"
create_database "$NOTIFICATION_DB_NAME" "$NOTIFICATION_DB_USER"

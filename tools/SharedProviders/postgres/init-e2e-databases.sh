#!/bin/sh
set -eu

create_database() {
    database_name="$1"
    role_name="$2"
    password_file="$3"
    role_password="$(cat "$password_file")"

    if [ -z "$role_password" ]; then
        echo "Role password file is empty." >&2
        exit 1
    fi

    psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=role_name="$role_name" --set=role_password="$role_password" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'role_name', :'role_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role_name')\gexec
SQL

    psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=database_name="$database_name" --set=role_name="$role_name" <<'SQL'
SELECT format('CREATE DATABASE %I OWNER %I', :'database_name', :'role_name')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database_name')\gexec
SELECT format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', :'database_name')\gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'database_name', :'role_name')\gexec
SQL
}

create_database candoitall_e2e_central candoitall_e2e_central /run/secrets/db-central-password
create_database candoitall_e2e_client_a candoitall_e2e_client_a /run/secrets/db-client-a-password
create_database candoitall_e2e_client_b candoitall_e2e_client_b /run/secrets/db-client-b-password

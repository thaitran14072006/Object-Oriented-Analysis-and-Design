#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C
shopt -s nullglob

sql() {
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver,1433 -U sa -C -b -l 10 "$@"
}

migrations=(/migrations/[0-9][0-9]_*/*.sql)
if (( ${#migrations[@]} == 0 )); then
    echo "No SQL migration files were mounted." >&2
    exit 1
fi

# The baseline consists of CREATE TABLE scripts for dbo.
expected=$(sed -nE 's/^CREATE TABLE (dbo\.)?\[?([A-Za-z0-9_]+)\]?[[:space:]]*\(.*/\2/p' "${migrations[@]}" | sort -u)
if [[ -z "$expected" ]]; then
    echo "No table definitions found in the SQL migrations." >&2
    exit 1
fi

sql -d master -Q "IF DB_ID(N'CarRental') IS NULL CREATE DATABASE [CarRental];"
current_tables() {
    sql -d CarRental -h -1 -W -Q "SET NOCOUNT ON; SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo');" \
        | tr -d '\r' | sed '/^[[:space:]]*$/d' | sort
}

actual=$(current_tables)
if [[ -z "$actual" ]]; then
    {
        cat <<'SQL'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource = 'RentalPro.Docker.Schema',
    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
IF @lockResult < 0
    THROW 51000, 'Could not acquire the schema initialization lock.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo'))
    THROW 51000, 'The schema changed during initialization. Retry deployment.', 1;
GO
SQL
        for migration in "${migrations[@]}"; do
            printf "PRINT N'Applying %s';\nGO\n:r %s\nGO\n" "$migration" "$migration"
        done
        printf 'COMMIT TRANSACTION;\nGO\n'
    } | sql -d CarRental -f 65001
    actual=$(current_tables)
fi

missing=$(comm -23 <(printf '%s\n' "$expected") <(printf '%s\n' "$actual"))
if [[ -n "$missing" ]]; then
    printf 'Existing CarRental schema is missing these tables:\n%s\n' "$missing" >&2
    exit 1
fi

printf 'CarRental schema ready: %s expected tables found.\n' "$(printf '%s\n' "$expected" | wc -l)"

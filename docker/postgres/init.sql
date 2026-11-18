-- PostgreSQL init script: creates the three service databases.
-- This runs as the postgres superuser when the container starts.
-- Each service owns exactly one database and never touches the others.

SELECT 'CREATE DATABASE identity_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'identity_db')\gexec

SELECT 'CREATE DATABASE inventory_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'inventory_db')\gexec

SELECT 'CREATE DATABASE orders_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'orders_db')\gexec

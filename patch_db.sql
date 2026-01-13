-- 错误说明: Npgsql.PostgresException (0x80004005): 42703: 字段 s.custom_configs 不存在
-- 修复方法: 执行以下 SQL 语句添加缺失的列

ALTER TABLE "sr_clients" ADD COLUMN "custom_configs" jsonb;

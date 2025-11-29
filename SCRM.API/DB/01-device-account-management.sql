-- =====================================================
-- 模块：设备与账号管理
-- 描述：用于管理设备授权、微信账号状态、设备版本等信息
-- 创建时间：2025-11-25
-- =====================================================

-- 1. 设备表
-- 用途：存储手机客户端、客服客户端等设备的基本信息
CREATE TABLE IF NOT EXISTS devices (
    device_id BIGSERIAL PRIMARY KEY,
    device_uuid VARCHAR(64) NOT NULL UNIQUE,
    device_type SMALLINT NOT NULL DEFAULT 1,
    device_name VARCHAR(100),
    os_type VARCHAR(20),
    os_version VARCHAR(50),
    device_model VARCHAR(100),
    device_brand VARCHAR(50),
    sdk_version VARCHAR(20),
    app_version VARCHAR(20),
    is_active BOOLEAN DEFAULT TRUE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

-- 创建索引
CREATE INDEX idx_devices_uuid ON devices(device_uuid);
CREATE INDEX idx_devices_type ON devices(device_type);
CREATE INDEX idx_devices_active ON devices(is_active, is_deleted);

-- 添加表注释
COMMENT ON TABLE devices IS '设备信息表';
COMMENT ON COLUMN devices.device_uuid IS '设备唯一标识（UUID/IMEI等）';
COMMENT ON COLUMN devices.device_type IS '设备类型：1-Android手机 2-iOS手机 3-Windows客服端 4-MacOS客服端 5-Web客服端';
COMMENT ON COLUMN devices.device_name IS '设备名称';
COMMENT ON COLUMN devices.os_type IS '操作系统类型：Android/iOS/Windows/MacOS';
COMMENT ON COLUMN devices.os_version IS '操作系统版本';
COMMENT ON COLUMN devices.device_model IS '设备型号';
COMMENT ON COLUMN devices.device_brand IS '设备品牌';
COMMENT ON COLUMN devices.sdk_version IS '当前SDK版本';
COMMENT ON COLUMN devices.app_version IS 'APP版本';
COMMENT ON COLUMN devices.is_active IS '是否激活';
COMMENT ON COLUMN devices.is_deleted IS '是否删除';
COMMENT ON COLUMN devices.created_at IS '创建时间';
COMMENT ON COLUMN devices.updated_at IS '更新时间';
COMMENT ON COLUMN devices.deleted_at IS '删除时间';

-- =====================================================

-- 2. 微信账号表
-- 用途：存储微信账号的基本信息
CREATE TABLE IF NOT EXISTS wechat_accounts (
    account_id BIGSERIAL PRIMARY KEY,
    wxid VARCHAR(100) NOT NULL UNIQUE,
    wechat_number VARCHAR(50),
    nickname VARCHAR(100),
    mobile_phone VARCHAR(20),
    gender SMALLINT DEFAULT 0,
    avatar_url VARCHAR(500),
    signature VARCHAR(500),
    qr_code_url VARCHAR(500),
    region VARCHAR(100),
    account_status SMALLINT DEFAULT 1,
    last_online_at TIMESTAMP,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

-- 创建索引
CREATE INDEX idx_wechat_accounts_wxid ON wechat_accounts(wxid);
CREATE INDEX idx_wechat_accounts_mobile ON wechat_accounts(mobile_phone);
CREATE INDEX idx_wechat_accounts_status ON wechat_accounts(account_status, is_deleted);

-- 添加表注释
COMMENT ON TABLE wechat_accounts IS '微信账号信息表';
COMMENT ON COLUMN wechat_accounts.wxid IS '微信WXID';
COMMENT ON COLUMN wechat_accounts.wechat_number IS '微信号';
COMMENT ON COLUMN wechat_accounts.nickname IS '微信昵称';
COMMENT ON COLUMN wechat_accounts.mobile_phone IS '手机号';
COMMENT ON COLUMN wechat_accounts.gender IS '性别：0-未知 1-男 2-女';
COMMENT ON COLUMN wechat_accounts.avatar_url IS '头像URL';
COMMENT ON COLUMN wechat_accounts.signature IS '个性签名';
COMMENT ON COLUMN wechat_accounts.qr_code_url IS '二维码URL';
COMMENT ON COLUMN wechat_accounts.region IS '地区';
COMMENT ON COLUMN wechat_accounts.account_status IS '账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常';
COMMENT ON COLUMN wechat_accounts.last_online_at IS '最后在线时间';
COMMENT ON COLUMN wechat_accounts.is_deleted IS '是否删除';
COMMENT ON COLUMN wechat_accounts.created_at IS '创建时间';
COMMENT ON COLUMN wechat_accounts.updated_at IS '更新时间';
COMMENT ON COLUMN wechat_accounts.deleted_at IS '删除时间';

-- =====================================================

-- 3. 设备授权表
-- 用途：管理设备与微信账号的授权关系及通信token
CREATE TABLE IF NOT EXISTS device_authorizations (
    auth_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL,
    account_id BIGINT NOT NULL,
    access_token VARCHAR(128) NOT NULL UNIQUE,
    token_expires_at TIMESTAMP NOT NULL,
    auth_status SMALLINT DEFAULT 1,
    auth_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    logout_time TIMESTAMP,
    last_active_at TIMESTAMP,
    client_ip VARCHAR(50),
    server_ip VARCHAR(50),
    server_port INT,
    logout_reason VARCHAR(200),
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_device_auth_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_device_auth_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_device_auth_device ON device_authorizations(device_id);
CREATE INDEX idx_device_auth_account ON device_authorizations(account_id);
CREATE INDEX idx_device_auth_token ON device_authorizations(access_token);
CREATE INDEX idx_device_auth_status ON device_authorizations(auth_status, is_deleted);
CREATE INDEX idx_device_auth_active ON device_authorizations(last_active_at);

-- 添加表注释
COMMENT ON TABLE device_authorizations IS '设备授权表';
COMMENT ON COLUMN device_authorizations.device_id IS '设备ID';
COMMENT ON COLUMN device_authorizations.account_id IS '微信账号ID';
COMMENT ON COLUMN device_authorizations.access_token IS '访问令牌';
COMMENT ON COLUMN device_authorizations.token_expires_at IS '令牌过期时间';
COMMENT ON COLUMN device_authorizations.auth_status IS '授权状态：1-已授权在线 2-正常退出 3-强制下线 4-令牌过期 5-账号异常';
COMMENT ON COLUMN device_authorizations.auth_time IS '授权时间';
COMMENT ON COLUMN device_authorizations.logout_time IS '退出时间';
COMMENT ON COLUMN device_authorizations.last_active_at IS '最后活跃时间';
COMMENT ON COLUMN device_authorizations.client_ip IS '客户端IP地址';
COMMENT ON COLUMN device_authorizations.server_ip IS '服务器IP地址';
COMMENT ON COLUMN device_authorizations.server_port IS '服务器端口';
COMMENT ON COLUMN device_authorizations.logout_reason IS '退出原因';
COMMENT ON COLUMN device_authorizations.is_deleted IS '是否删除';
COMMENT ON COLUMN device_authorizations.created_at IS '创建时间';
COMMENT ON COLUMN device_authorizations.updated_at IS '更新时间';

-- =====================================================

-- 4. 账号状态日志表
-- 用途：记录微信账号的状态变化历史（上线、下线、登出等）
CREATE TABLE IF NOT EXISTS account_status_logs (
    log_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL,
    device_id BIGINT,
    auth_id BIGINT,
    status_type SMALLINT NOT NULL,
    previous_status SMALLINT,
    current_status SMALLINT,
    client_ip VARCHAR(50),
    reason VARCHAR(500),
    occurred_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_status_log_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_status_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_status_log_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id)
);

-- 创建索引
CREATE INDEX idx_status_logs_account ON account_status_logs(account_id);
CREATE INDEX idx_status_logs_device ON account_status_logs(device_id);
CREATE INDEX idx_status_logs_type ON account_status_logs(status_type);
CREATE INDEX idx_status_logs_time ON account_status_logs(occurred_at);

-- 添加表注释
COMMENT ON TABLE account_status_logs IS '账号状态日志表';
COMMENT ON COLUMN account_status_logs.account_id IS '微信账号ID';
COMMENT ON COLUMN account_status_logs.device_id IS '设备ID';
COMMENT ON COLUMN account_status_logs.auth_id IS '授权记录ID';
COMMENT ON COLUMN account_status_logs.status_type IS '状态类型：1-上线 2-下线 3-主动登出 4-强制下线 5-令牌过期 6-网络断开';
COMMENT ON COLUMN account_status_logs.previous_status IS '之前状态';
COMMENT ON COLUMN account_status_logs.current_status IS '当前状态';
COMMENT ON COLUMN account_status_logs.client_ip IS 'IP地址';
COMMENT ON COLUMN account_status_logs.reason IS '原因/备注';
COMMENT ON COLUMN account_status_logs.occurred_at IS '发生时间';
COMMENT ON COLUMN account_status_logs.created_at IS '创建时间';

-- =====================================================

-- 5. 设备版本记录表
-- 用途：记录设备SDK版本的上报历史
CREATE TABLE IF NOT EXISTS device_version_logs (
    log_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL,
    sdk_version VARCHAR(20) NOT NULL,
    app_version VARCHAR(20),
    os_version VARCHAR(50),
    reported_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_version_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

-- 创建索引
CREATE INDEX idx_version_logs_device ON device_version_logs(device_id);
CREATE INDEX idx_version_logs_time ON device_version_logs(reported_at);

-- 添加表注释
COMMENT ON TABLE device_version_logs IS '设备版本记录表';
COMMENT ON COLUMN device_version_logs.device_id IS '设备ID';
COMMENT ON COLUMN device_version_logs.sdk_version IS 'SDK版本号';
COMMENT ON COLUMN device_version_logs.app_version IS 'APP版本号';
COMMENT ON COLUMN device_version_logs.os_version IS '操作系统版本';
COMMENT ON COLUMN device_version_logs.reported_at IS '上报时间';
COMMENT ON COLUMN device_version_logs.created_at IS '创建时间';

-- =====================================================

-- 6. 服务器重定向记录表
-- 用途：记录设备连接服务器的重定向历史
CREATE TABLE IF NOT EXISTS server_redirects (
    redirect_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL,
    auth_id BIGINT,
    old_server_ip VARCHAR(50),
    old_server_port INT,
    new_server_ip VARCHAR(50) NOT NULL,
    new_server_port INT NOT NULL,
    redirect_reason VARCHAR(200),
    redirect_type SMALLINT DEFAULT 1,
    redirected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_redirect_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_redirect_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id)
);

-- 创建索引
CREATE INDEX idx_redirects_device ON server_redirects(device_id);
CREATE INDEX idx_redirects_time ON server_redirects(redirected_at);

-- 添加表注释
COMMENT ON TABLE server_redirects IS '服务器重定向记录表';
COMMENT ON COLUMN server_redirects.device_id IS '设备ID';
COMMENT ON COLUMN server_redirects.auth_id IS '授权记录ID';
COMMENT ON COLUMN server_redirects.old_server_ip IS '原服务器IP';
COMMENT ON COLUMN server_redirects.old_server_port IS '原服务器端口';
COMMENT ON COLUMN server_redirects.new_server_ip IS '新服务器IP';
COMMENT ON COLUMN server_redirects.new_server_port IS '新服务器端口';
COMMENT ON COLUMN server_redirects.redirect_reason IS '重定向原因';
COMMENT ON COLUMN server_redirects.redirect_type IS '重定向类型：1-负载均衡 2-服务器维护 3-异常切换';
COMMENT ON COLUMN server_redirects.redirected_at IS '重定向时间';
COMMENT ON COLUMN server_redirects.created_at IS '创建时间';

-- =====================================================

-- 7. 设备心跳记录表
-- 用途：记录设备的心跳包，用于监控设备在线状态
CREATE TABLE IF NOT EXISTS device_heartbeats (
    heartbeat_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL,
    auth_id BIGINT,
    client_ip VARCHAR(50),
    heartbeat_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_heartbeat_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_heartbeat_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id)
);

-- 创建索引
CREATE INDEX idx_heartbeats_device ON device_heartbeats(device_id);
CREATE INDEX idx_heartbeats_time ON device_heartbeats(heartbeat_at);
CREATE INDEX idx_heartbeats_device_time ON device_heartbeats(device_id, heartbeat_at DESC);

-- 添加表注释
COMMENT ON TABLE device_heartbeats IS '设备心跳记录表';
COMMENT ON COLUMN device_heartbeats.device_id IS '设备ID';
COMMENT ON COLUMN device_heartbeats.auth_id IS '授权记录ID';
COMMENT ON COLUMN device_heartbeats.client_ip IS '客户端IP';
COMMENT ON COLUMN device_heartbeats.heartbeat_at IS '心跳时间';
COMMENT ON COLUMN device_heartbeats.created_at IS '创建时间';

-- =====================================================
-- 创建更新时间触发器函数
-- =====================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 为需要自动更新updated_at的表创建触发器
CREATE TRIGGER trigger_devices_updated_at
    BEFORE UPDATE ON devices
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_wechat_accounts_updated_at
    BEFORE UPDATE ON wechat_accounts
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_device_authorizations_updated_at
    BEFORE UPDATE ON device_authorizations
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 初始化数据（可选）
-- =====================================================

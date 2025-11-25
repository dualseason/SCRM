-- =====================================================
-- 模块：设备与账号管理
-- 描述：用于管理设备授权、微信账号状态、设备版本等信息
-- 创建时间：2025-11-25
-- =====================================================

-- 1. 设备表
-- 用途：存储手机客户端、客服客户端等设备的基本信息
CREATE TABLE IF NOT EXISTS devices (
    device_id BIGSERIAL PRIMARY KEY,
    device_uuid VARCHAR(64) NOT NULL UNIQUE COMMENT '设备唯一标识（UUID/IMEI等）',
    device_type SMALLINT NOT NULL DEFAULT 1 COMMENT '设备类型：1-手机客户端 2-客服客户端',
    device_name VARCHAR(100) COMMENT '设备名称',
    os_type VARCHAR(20) COMMENT '操作系统类型：Android/iOS/Windows/MacOS',
    os_version VARCHAR(50) COMMENT '操作系统版本',
    device_model VARCHAR(100) COMMENT '设备型号',
    device_brand VARCHAR(50) COMMENT '设备品牌',
    sdk_version VARCHAR(20) COMMENT '当前SDK版本',
    app_version VARCHAR(20) COMMENT 'APP版本',
    is_active BOOLEAN DEFAULT TRUE COMMENT '是否激活',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
    deleted_at TIMESTAMP COMMENT '删除时间'
);

-- 创建索引
CREATE INDEX idx_devices_uuid ON devices(device_uuid);
CREATE INDEX idx_devices_type ON devices(device_type);
CREATE INDEX idx_devices_active ON devices(is_active, is_deleted);

COMMENT ON TABLE devices IS '设备信息表';

-- =====================================================

-- 2. 微信账号表
-- 用途：存储微信账号的基本信息
CREATE TABLE IF NOT EXISTS wechat_accounts (
    account_id BIGSERIAL PRIMARY KEY,
    wxid VARCHAR(100) NOT NULL UNIQUE COMMENT '微信WXID',
    wechat_number VARCHAR(50) COMMENT '微信号',
    nickname VARCHAR(100) COMMENT '微信昵称',
    mobile_phone VARCHAR(20) COMMENT '手机号',
    gender SMALLINT DEFAULT 0 COMMENT '性别：0-未知 1-男 2-女',
    avatar_url VARCHAR(500) COMMENT '头像URL',
    signature VARCHAR(500) COMMENT '个性签名',
    qr_code_url VARCHAR(500) COMMENT '二维码URL',
    region VARCHAR(100) COMMENT '地区',
    account_status SMALLINT DEFAULT 1 COMMENT '账号状态：1-正常 2-冻结 3-注销 4-异常',
    last_online_at TIMESTAMP COMMENT '最后在线时间',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
    deleted_at TIMESTAMP COMMENT '删除时间'
);

-- 创建索引
CREATE INDEX idx_wechat_accounts_wxid ON wechat_accounts(wxid);
CREATE INDEX idx_wechat_accounts_mobile ON wechat_accounts(mobile_phone);
CREATE INDEX idx_wechat_accounts_status ON wechat_accounts(account_status, is_deleted);

COMMENT ON TABLE wechat_accounts IS '微信账号信息表';

-- =====================================================

-- 3. 设备授权表
-- 用途：管理设备与微信账号的授权关系及通信token
CREATE TABLE IF NOT EXISTS device_authorizations (
    auth_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL COMMENT '设备ID',
    account_id BIGINT NOT NULL COMMENT '微信账号ID',
    access_token VARCHAR(128) NOT NULL UNIQUE COMMENT '访问令牌',
    token_expires_at TIMESTAMP NOT NULL COMMENT '令牌过期时间',
    auth_status SMALLINT DEFAULT 1 COMMENT '授权状态：1-已授权 2-已退出 3-强制下线 4-令牌过期',
    auth_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '授权时间',
    logout_time TIMESTAMP COMMENT '退出时间',
    last_active_at TIMESTAMP COMMENT '最后活跃时间',
    client_ip VARCHAR(50) COMMENT '客户端IP地址',
    server_ip VARCHAR(50) COMMENT '服务器IP地址',
    server_port INT COMMENT '服务器端口',
    logout_reason VARCHAR(200) COMMENT '退出原因',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_device_auth_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_device_auth_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_device_auth_device ON device_authorizations(device_id);
CREATE INDEX idx_device_auth_account ON device_authorizations(account_id);
CREATE INDEX idx_device_auth_token ON device_authorizations(access_token);
CREATE INDEX idx_device_auth_status ON device_authorizations(auth_status, is_deleted);
CREATE INDEX idx_device_auth_active ON device_authorizations(last_active_at);

COMMENT ON TABLE device_authorizations IS '设备授权表';

-- =====================================================

-- 4. 账号状态日志表
-- 用途：记录微信账号的状态变化历史（上线、下线、登出等）
CREATE TABLE IF NOT EXISTS account_status_logs (
    log_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '微信账号ID',
    device_id BIGINT COMMENT '设备ID',
    auth_id BIGINT COMMENT '授权记录ID',
    status_type SMALLINT NOT NULL COMMENT '状态类型：1-上线 2-下线 3-登出 4-强制下线',
    previous_status SMALLINT COMMENT '之前状态',
    current_status SMALLINT COMMENT '当前状态',
    client_ip VARCHAR(50) COMMENT 'IP地址',
    reason VARCHAR(500) COMMENT '原因/备注',
    occurred_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '发生时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_status_log_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_status_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_status_log_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id)
);

-- 创建索引
CREATE INDEX idx_status_logs_account ON account_status_logs(account_id);
CREATE INDEX idx_status_logs_device ON account_status_logs(device_id);
CREATE INDEX idx_status_logs_type ON account_status_logs(status_type);
CREATE INDEX idx_status_logs_time ON account_status_logs(occurred_at);

COMMENT ON TABLE account_status_logs IS '账号状态日志表';

-- =====================================================

-- 5. 设备版本记录表
-- 用途：记录设备SDK版本的上报历史
CREATE TABLE IF NOT EXISTS device_version_logs (
    log_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL COMMENT '设备ID',
    sdk_version VARCHAR(20) NOT NULL COMMENT 'SDK版本号',
    app_version VARCHAR(20) COMMENT 'APP版本号',
    os_version VARCHAR(50) COMMENT '操作系统版本',
    reported_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '上报时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_version_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

-- 创建索引
CREATE INDEX idx_version_logs_device ON device_version_logs(device_id);
CREATE INDEX idx_version_logs_time ON device_version_logs(reported_at);

COMMENT ON TABLE device_version_logs IS '设备版本记录表';

-- =====================================================

-- 6. 服务器重定向记录表
-- 用途：记录设备连接服务器的重定向历史
CREATE TABLE IF NOT EXISTS server_redirects (
    redirect_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL COMMENT '设备ID',
    auth_id BIGINT COMMENT '授权记录ID',
    old_server_ip VARCHAR(50) COMMENT '原服务器IP',
    old_server_port INT COMMENT '原服务器端口',
    new_server_ip VARCHAR(50) NOT NULL COMMENT '新服务器IP',
    new_server_port INT NOT NULL COMMENT '新服务器端口',
    redirect_reason VARCHAR(200) COMMENT '重定向原因',
    redirect_type SMALLINT DEFAULT 1 COMMENT '重定向类型：1-负载均衡 2-服务器维护 3-异常切换',
    redirected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '重定向时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_redirect_device FOREIGN KEY (device_id) REFERENCES devices(device_id),
    CONSTRAINT fk_redirect_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id)
);

-- 创建索引
CREATE INDEX idx_redirects_device ON server_redirects(device_id);
CREATE INDEX idx_redirects_time ON server_redirects(redirected_at);

COMMENT ON TABLE server_redirects IS '服务器重定向记录表';

-- =====================================================

-- 7. 设备心跳记录表
-- 用途：记录设备的心跳包，用于监控设备在线状态
CREATE TABLE IF NOT EXISTS device_heartbeats (
    heartbeat_id BIGSERIAL PRIMARY KEY,
    device_id BIGINT NOT NULL COMMENT '设备ID',
    auth_id BIGINT COMMENT '授权记录ID',
    client_ip VARCHAR(50) COMMENT '客户端IP',
    heartbeat_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '心跳时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间'
);

-- 创建索引
CREATE INDEX idx_heartbeats_device ON device_heartbeats(device_id);
CREATE INDEX idx_heartbeats_time ON device_heartbeats(heartbeat_at);
CREATE INDEX idx_heartbeats_device_time ON device_heartbeats(device_id, heartbeat_at DESC);

COMMENT ON TABLE device_heartbeats IS '设备心跳记录表';

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

-- 插入默认设备类型说明
COMMENT ON COLUMN devices.device_type IS '设备类型：1-Android手机 2-iOS手机 3-Windows客服端 4-MacOS客服端 5-Web客服端';
COMMENT ON COLUMN wechat_accounts.account_status IS '账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常';
COMMENT ON COLUMN device_authorizations.auth_status IS '授权状态：1-已授权在线 2-正常退出 3-强制下线 4-令牌过期 5-账号异常';
COMMENT ON COLUMN account_status_logs.status_type IS '状态类型：1-上线 2-下线 3-主动登出 4-强制下线 5-令牌过期 6-网络断开';

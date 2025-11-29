-- =====================================================
-- 模块：消息通信
-- 描述：用于管理微信消息的发送、接收、存储及各种消息类型的处理
-- 创建时间：2025-11-25
-- =====================================================

-- 1. 聊天消息表
-- 用途：存储所有聊天消息的基本信息（核心表）
CREATE TABLE IF NOT EXISTS messages (
    message_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL,
    msg_svr_id BIGINT,
    conversation_id BIGINT,
    sender_id BIGINT,
    sender_wxid VARCHAR(100),
    receiver_id BIGINT,
    receiver_wxid VARCHAR(100),
    chat_type SMALLINT NOT NULL DEFAULT 1,
    message_type SMALLINT NOT NULL,
    content TEXT,
    content_xml TEXT,
    direction SMALLINT NOT NULL,
    send_status SMALLINT DEFAULT 1,
    read_status SMALLINT DEFAULT 0,
    is_revoked BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,
    local_message_id VARCHAR(100),
    client_msg_id VARCHAR(100),
    sent_at TIMESTAMP,
    received_at TIMESTAMP,
    read_at TIMESTAMP,
    revoked_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_message_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_message_sender FOREIGN KEY (sender_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_message_receiver FOREIGN KEY (receiver_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_messages_account ON messages(account_id);
CREATE INDEX IF NOT EXISTS idx_messages_msg_svr_id ON messages(msg_svr_id);
CREATE INDEX IF NOT EXISTS idx_messages_conversation ON messages(conversation_id);
CREATE INDEX IF NOT EXISTS idx_messages_sender ON messages(sender_id);
CREATE INDEX IF NOT EXISTS idx_messages_receiver ON messages(receiver_id);
CREATE INDEX IF NOT EXISTS idx_messages_sender_wxid ON messages(sender_wxid);
CREATE INDEX IF NOT EXISTS idx_messages_receiver_wxid ON messages(receiver_wxid);
CREATE INDEX IF NOT EXISTS idx_messages_chat_type ON messages(chat_type);
CREATE INDEX IF NOT EXISTS idx_messages_type ON messages(message_type);
CREATE INDEX IF NOT EXISTS idx_messages_direction ON messages(direction);
CREATE INDEX IF NOT EXISTS idx_messages_status ON messages(send_status, read_status);
CREATE INDEX IF NOT EXISTS idx_messages_sent_at ON messages(sent_at);
CREATE INDEX IF NOT EXISTS idx_messages_received_at ON messages(received_at);
CREATE INDEX IF NOT EXISTS idx_messages_revoked ON messages(is_revoked);
CREATE INDEX IF NOT EXISTS idx_messages_deleted ON messages(is_deleted);

COMMENT ON TABLE messages IS '聊天消息表';

-- =====================================================

-- 2. 消息媒体文件表
-- 用途：存储消息中的图片、视频、语音、文件等媒体资源
CREATE TABLE IF NOT EXISTS message_media (
    media_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL,
    media_type SMALLINT NOT NULL,
    file_name VARCHAR(255),
    file_size BIGINT,
    file_path VARCHAR(500),
    file_url VARCHAR(1000),
    cdn_url VARCHAR(1000),
    mime_type VARCHAR(100),
    file_md5 VARCHAR(64),
    duration INT,
    width INT,
    height INT,
    thumbnail_url VARCHAR(1000),
    download_status SMALLINT DEFAULT 0,
    upload_status SMALLINT DEFAULT 0,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_media_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_media_message ON message_media(message_id);
CREATE INDEX IF NOT EXISTS idx_media_type ON message_media(media_type);
CREATE INDEX IF NOT EXISTS idx_media_md5 ON message_media(file_md5);
CREATE INDEX IF NOT EXISTS idx_media_download ON message_media(download_status);
CREATE INDEX IF NOT EXISTS idx_media_upload ON message_media(upload_status);

COMMENT ON TABLE message_media IS '消息媒体文件表';

-- =====================================================

-- 3. 消息扩展信息表
-- 用途：存储特殊类型消息的扩展信息（名片、位置、链接、小程序等）
CREATE TABLE IF NOT EXISTS message_extensions (
    extension_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL,
    extension_type SMALLINT NOT NULL,
    title VARCHAR(500),
    description TEXT,
    url VARCHAR(1000),
    thumb_url VARCHAR(1000),

    -- 名片信息
    card_wxid VARCHAR(100),
    card_nickname VARCHAR(100),
    card_avatar VARCHAR(500),

    -- 位置信息
    latitude DECIMAL(10, 7),
    longitude DECIMAL(10, 7),
    location_label VARCHAR(200),
    location_address VARCHAR(500),

    -- 小程序信息
    appid VARCHAR(100),
    page_path VARCHAR(500),

    -- 红包/转账信息
    money_amount DECIMAL(10, 2),
    money_type SMALLINT,
    money_status SMALLINT,

    -- 表情信息
    emoji_md5 VARCHAR(64),
    emoji_url VARCHAR(1000),

    extra_data JSONB,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_extension_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_extension_message ON message_extensions(message_id);
CREATE INDEX IF NOT EXISTS idx_extension_type ON message_extensions(extension_type);
CREATE INDEX IF NOT EXISTS idx_extension_card_wxid ON message_extensions(card_wxid);
CREATE INDEX IF NOT EXISTS idx_extension_appid ON message_extensions(appid);

COMMENT ON TABLE message_extensions IS '消息扩展信息表';

-- =====================================================

-- 4. 消息撤回记录表
-- 用途：记录消息撤回的历史信息
CREATE TABLE IF NOT EXISTS message_revocations (
    revocation_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL,
    account_id BIGINT NOT NULL,
    operator_wxid VARCHAR(100),
    revoke_type SMALLINT DEFAULT 1,
    original_content TEXT,
    revoke_reason VARCHAR(200),
    revoked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_revocation_message FOREIGN KEY (message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_revocation_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_revocation_message ON message_revocations(message_id);
CREATE INDEX IF NOT EXISTS idx_revocation_account ON message_revocations(account_id);
CREATE INDEX IF NOT EXISTS idx_revocation_time ON message_revocations(revoked_at);

COMMENT ON TABLE message_revocations IS '消息撤回记录表';

-- =====================================================

-- 5. 消息转发记录表
-- 用途：记录消息转发的历史（包括单条转发和合并转发）
CREATE TABLE IF NOT EXISTS message_forwards (
    forward_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL,
    source_message_id BIGINT,
    target_message_id BIGINT,
    forward_type SMALLINT NOT NULL,
    source_count INT DEFAULT 1,
    target_wxid VARCHAR(100),
    target_type SMALLINT,
    forward_status SMALLINT DEFAULT 1,
    error_message VARCHAR(500),
    forwarded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_forward_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_forward_source_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_forward_target_message FOREIGN KEY (target_message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_forward_account ON message_forwards(account_id);
CREATE INDEX IF NOT EXISTS idx_forward_source ON message_forwards(source_message_id);
CREATE INDEX IF NOT EXISTS idx_forward_target ON message_forwards(target_message_id);
CREATE INDEX IF NOT EXISTS idx_forward_type ON message_forwards(forward_type);
CREATE INDEX IF NOT EXISTS idx_forward_status ON message_forwards(forward_status);
CREATE INDEX IF NOT EXISTS idx_forward_time ON message_forwards(forwarded_at);

COMMENT ON TABLE message_forwards IS '消息转发记录表';

-- =====================================================

-- 6. 消息转发详情表
-- 用途：记录合并转发时的详细消息列表
CREATE TABLE IF NOT EXISTS message_forward_details (
    detail_id BIGSERIAL PRIMARY KEY,
    forward_id BIGINT NOT NULL,
    source_message_id BIGINT NOT NULL,
    sort_order INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_forward_detail_forward FOREIGN KEY (forward_id) REFERENCES message_forwards(forward_id),
    CONSTRAINT fk_forward_detail_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_forward_detail_forward ON message_forward_details(forward_id);
CREATE INDEX IF NOT EXISTS idx_forward_detail_message ON message_forward_details(source_message_id);

COMMENT ON TABLE message_forward_details IS '消息转发详情表';

-- =====================================================

-- 7. 语音转文字记录表
-- 用途：记录语音消息转文字的结果
CREATE TABLE IF NOT EXISTS voice_to_text_logs (
    log_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL,
    account_id BIGINT NOT NULL,
    media_id BIGINT,
    text_content TEXT,
    confidence DECIMAL(5, 4),
    language VARCHAR(20) DEFAULT 'zh-CN',
    duration INT,
    convert_status SMALLINT DEFAULT 1,
    error_message VARCHAR(500),
    converted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_voice_message FOREIGN KEY (message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_voice_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_voice_media FOREIGN KEY (media_id) REFERENCES message_media(media_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_voice_message ON voice_to_text_logs(message_id);
CREATE INDEX IF NOT EXISTS idx_voice_account ON voice_to_text_logs(account_id);
CREATE INDEX IF NOT EXISTS idx_voice_status ON voice_to_text_logs(convert_status);
CREATE INDEX IF NOT EXISTS idx_voice_time ON voice_to_text_logs(converted_at);

COMMENT ON TABLE voice_to_text_logs IS '语音转文字记录表';

-- =====================================================

-- 8. 群发消息记录表
-- 用途：记录群发消息任务及执行情况
CREATE TABLE IF NOT EXISTS mass_messages (
    mass_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL,
    message_type SMALLINT NOT NULL,
    content TEXT,
    target_count INT DEFAULT 0,
    success_count INT DEFAULT 0,
    fail_count INT DEFAULT 0,
    mass_status SMALLINT DEFAULT 1,
    scheduled_at TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_mass_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_mass_account ON mass_messages(account_id);
CREATE INDEX IF NOT EXISTS idx_mass_status ON mass_messages(mass_status);
CREATE INDEX IF NOT EXISTS idx_mass_scheduled ON mass_messages(scheduled_at);
CREATE INDEX IF NOT EXISTS idx_mass_started ON mass_messages(started_at);

COMMENT ON TABLE mass_messages IS '群发消息记录表';

-- =====================================================

-- 9. 群发消息详情表
-- 用途：记录群发消息的每个目标的发送详情
CREATE TABLE IF NOT EXISTS mass_message_details (
    detail_id BIGSERIAL PRIMARY KEY,
    mass_id BIGINT NOT NULL,
    target_wxid VARCHAR(100) NOT NULL,
    target_type SMALLINT,
    message_id BIGINT,
    send_status SMALLINT DEFAULT 1,
    error_message VARCHAR(500),
    sent_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_mass_detail_mass FOREIGN KEY (mass_id) REFERENCES mass_messages(mass_id),
    CONSTRAINT fk_mass_detail_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_mass_detail_mass ON mass_message_details(mass_id);
CREATE INDEX IF NOT EXISTS idx_mass_detail_target ON mass_message_details(target_wxid);
CREATE INDEX IF NOT EXISTS idx_mass_detail_status ON mass_message_details(send_status);
CREATE INDEX IF NOT EXISTS idx_mass_detail_message ON mass_message_details(message_id);

COMMENT ON TABLE mass_message_details IS '群发消息详情表';

-- =====================================================

-- 10. 消息同步日志表
-- 用途：记录消息同步任务（按时间段拉取历史消息）
CREATE TABLE IF NOT EXISTS message_sync_logs (
    log_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL,
    device_id BIGINT,
    sync_type SMALLINT DEFAULT 1,
    start_time TIMESTAMP,
    end_time TIMESTAMP,
    total_count INT DEFAULT 0,
    success_count INT DEFAULT 0,
    fail_count INT DEFAULT 0,
    sync_status SMALLINT DEFAULT 1,
    error_message TEXT,
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_sync_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_sync_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_sync_account ON message_sync_logs(account_id);
CREATE INDEX IF NOT EXISTS idx_sync_device ON message_sync_logs(device_id);
CREATE INDEX IF NOT EXISTS idx_sync_status ON message_sync_logs(sync_status);
CREATE INDEX IF NOT EXISTS idx_sync_time ON message_sync_logs(start_time, end_time);

COMMENT ON TABLE message_sync_logs IS '消息同步日志表';



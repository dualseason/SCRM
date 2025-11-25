-- =====================================================
-- 模块：消息通信
-- 描述：用于管理微信消息的发送、接收、存储及各种消息类型的处理
-- 创建时间：2025-11-25
-- =====================================================

-- 1. 聊天消息表
-- 用途：存储所有聊天消息的基本信息（核心表）
CREATE TABLE IF NOT EXISTS messages (
    message_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    msg_svr_id BIGINT COMMENT '微信服务器消息ID',
    conversation_id BIGINT COMMENT '会话ID',
    sender_id BIGINT COMMENT '发送者账号ID',
    sender_wxid VARCHAR(100) COMMENT '发送者WXID',
    receiver_id BIGINT COMMENT '接收者账号ID',
    receiver_wxid VARCHAR(100) COMMENT '接收者WXID',
    chat_type SMALLINT NOT NULL DEFAULT 1 COMMENT '聊天类型：1-单聊 2-群聊 3-公众号',
    message_type SMALLINT NOT NULL COMMENT '消息类型：1-文本 2-图片 3-语音 4-视频 5-文件 6-名片 7-位置 8-链接 9-小程序 10-表情 11-系统消息 12-红包 13-转账 14-合并转发',
    content TEXT COMMENT '消息内容（文本消息/JSON格式）',
    content_xml TEXT COMMENT '消息原始XML数据',
    direction SMALLINT NOT NULL COMMENT '消息方向：1-发出 2-接收',
    send_status SMALLINT DEFAULT 1 COMMENT '发送状态：1-发送中 2-发送成功 3-发送失败',
    read_status SMALLINT DEFAULT 0 COMMENT '阅读状态：0-未读 1-已读',
    is_revoked BOOLEAN DEFAULT FALSE COMMENT '是否已撤回',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否已删除',
    local_message_id VARCHAR(100) COMMENT '本地消息ID',
    client_msg_id VARCHAR(100) COMMENT '客户端消息ID',
    sent_at TIMESTAMP COMMENT '发送时间',
    received_at TIMESTAMP COMMENT '接收时间',
    read_at TIMESTAMP COMMENT '阅读时间',
    revoked_at TIMESTAMP COMMENT '撤回时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_message_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_message_sender FOREIGN KEY (sender_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_message_receiver FOREIGN KEY (receiver_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_messages_account ON messages(account_id);
CREATE INDEX idx_messages_msg_svr_id ON messages(msg_svr_id);
CREATE INDEX idx_messages_conversation ON messages(conversation_id);
CREATE INDEX idx_messages_sender ON messages(sender_id);
CREATE INDEX idx_messages_receiver ON messages(receiver_id);
CREATE INDEX idx_messages_sender_wxid ON messages(sender_wxid);
CREATE INDEX idx_messages_receiver_wxid ON messages(receiver_wxid);
CREATE INDEX idx_messages_chat_type ON messages(chat_type);
CREATE INDEX idx_messages_type ON messages(message_type);
CREATE INDEX idx_messages_direction ON messages(direction);
CREATE INDEX idx_messages_status ON messages(send_status, read_status);
CREATE INDEX idx_messages_sent_at ON messages(sent_at);
CREATE INDEX idx_messages_received_at ON messages(received_at);
CREATE INDEX idx_messages_revoked ON messages(is_revoked);
CREATE INDEX idx_messages_deleted ON messages(is_deleted);

COMMENT ON TABLE messages IS '聊天消息表';

-- =====================================================

-- 2. 消息媒体文件表
-- 用途：存储消息中的图片、视频、语音、文件等媒体资源
CREATE TABLE IF NOT EXISTS message_media (
    media_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL COMMENT '关联消息ID',
    media_type SMALLINT NOT NULL COMMENT '媒体类型：1-图片 2-语音 3-视频 4-文件 5-缩略图',
    file_name VARCHAR(255) COMMENT '文件名',
    file_size BIGINT COMMENT '文件大小（字节）',
    file_path VARCHAR(500) COMMENT '文件存储路径',
    file_url VARCHAR(1000) COMMENT '文件URL',
    cdn_url VARCHAR(1000) COMMENT 'CDN地址',
    mime_type VARCHAR(100) COMMENT 'MIME类型',
    file_md5 VARCHAR(64) COMMENT '文件MD5值',
    duration INT COMMENT '时长（秒，用于语音/视频）',
    width INT COMMENT '宽度（像素，用于图片/视频）',
    height INT COMMENT '高度（像素，用于图片/视频）',
    thumbnail_url VARCHAR(1000) COMMENT '缩略图URL',
    download_status SMALLINT DEFAULT 0 COMMENT '下载状态：0-未下载 1-下载中 2-已下载 3-下载失败',
    upload_status SMALLINT DEFAULT 0 COMMENT '上传状态：0-未上传 1-上传中 2-已上传 3-上传失败',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_media_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX idx_media_message ON message_media(message_id);
CREATE INDEX idx_media_type ON message_media(media_type);
CREATE INDEX idx_media_md5 ON message_media(file_md5);
CREATE INDEX idx_media_download ON message_media(download_status);
CREATE INDEX idx_media_upload ON message_media(upload_status);

COMMENT ON TABLE message_media IS '消息媒体文件表';

-- =====================================================

-- 3. 消息扩展信息表
-- 用途：存储特殊类型消息的扩展信息（名片、位置、链接、小程序等）
CREATE TABLE IF NOT EXISTS message_extensions (
    extension_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL COMMENT '关联消息ID',
    extension_type SMALLINT NOT NULL COMMENT '扩展类型：1-名片 2-位置 3-链接 4-小程序 5-红包 6-转账 7-表情',
    title VARCHAR(500) COMMENT '标题',
    description TEXT COMMENT '描述',
    url VARCHAR(1000) COMMENT 'URL地址',
    thumb_url VARCHAR(1000) COMMENT '缩略图URL',

    -- 名片信息
    card_wxid VARCHAR(100) COMMENT '名片微信WXID',
    card_nickname VARCHAR(100) COMMENT '名片昵称',
    card_avatar VARCHAR(500) COMMENT '名片头像',

    -- 位置信息
    latitude DECIMAL(10, 7) COMMENT '纬度',
    longitude DECIMAL(10, 7) COMMENT '经度',
    location_label VARCHAR(200) COMMENT '位置标签',
    location_address VARCHAR(500) COMMENT '详细地址',

    -- 小程序信息
    appid VARCHAR(100) COMMENT '小程序AppID',
    page_path VARCHAR(500) COMMENT '小程序页面路径',

    -- 红包/转账信息
    money_amount DECIMAL(10, 2) COMMENT '金额',
    money_type SMALLINT COMMENT '类型：1-红包 2-转账',
    money_status SMALLINT COMMENT '状态：1-待领取 2-已领取 3-已退回',

    -- 表情信息
    emoji_md5 VARCHAR(64) COMMENT '表情MD5',
    emoji_url VARCHAR(1000) COMMENT '表情URL',

    extra_data JSONB COMMENT '额外数据（JSON格式）',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_extension_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX idx_extension_message ON message_extensions(message_id);
CREATE INDEX idx_extension_type ON message_extensions(extension_type);
CREATE INDEX idx_extension_card_wxid ON message_extensions(card_wxid);
CREATE INDEX idx_extension_appid ON message_extensions(appid);

COMMENT ON TABLE message_extensions IS '消息扩展信息表';

-- =====================================================

-- 4. 消息撤回记录表
-- 用途：记录消息撤回的历史信息
CREATE TABLE IF NOT EXISTS message_revocations (
    revocation_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL COMMENT '被撤回的消息ID',
    account_id BIGINT NOT NULL COMMENT '撤回操作的账号ID',
    operator_wxid VARCHAR(100) COMMENT '操作者WXID',
    revoke_type SMALLINT DEFAULT 1 COMMENT '撤回类型：1-主动撤回 2-被动接收撤回通知',
    original_content TEXT COMMENT '原始消息内容',
    revoke_reason VARCHAR(200) COMMENT '撤回原因',
    revoked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '撤回时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_revocation_message FOREIGN KEY (message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_revocation_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_revocation_message ON message_revocations(message_id);
CREATE INDEX idx_revocation_account ON message_revocations(account_id);
CREATE INDEX idx_revocation_time ON message_revocations(revoked_at);

COMMENT ON TABLE message_revocations IS '消息撤回记录表';

-- =====================================================

-- 5. 消息转发记录表
-- 用途：记录消息转发的历史（包括单条转发和合并转发）
CREATE TABLE IF NOT EXISTS message_forwards (
    forward_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '转发操作的账号ID',
    source_message_id BIGINT COMMENT '源消息ID（单条转发）',
    target_message_id BIGINT COMMENT '转发后生成的新消息ID',
    forward_type SMALLINT NOT NULL COMMENT '转发类型：1-单条转发 2-合并转发 3-逐条转发',
    source_count INT DEFAULT 1 COMMENT '转发消息数量',
    target_wxid VARCHAR(100) COMMENT '转发目标WXID',
    target_type SMALLINT COMMENT '目标类型：1-好友 2-群聊',
    forward_status SMALLINT DEFAULT 1 COMMENT '转发状态：1-转发中 2-成功 3-失败',
    error_message VARCHAR(500) COMMENT '错误信息',
    forwarded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '转发时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_forward_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_forward_source_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_forward_target_message FOREIGN KEY (target_message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX idx_forward_account ON message_forwards(account_id);
CREATE INDEX idx_forward_source ON message_forwards(source_message_id);
CREATE INDEX idx_forward_target ON message_forwards(target_message_id);
CREATE INDEX idx_forward_type ON message_forwards(forward_type);
CREATE INDEX idx_forward_status ON message_forwards(forward_status);
CREATE INDEX idx_forward_time ON message_forwards(forwarded_at);

COMMENT ON TABLE message_forwards IS '消息转发记录表';

-- =====================================================

-- 6. 消息转发详情表
-- 用途：记录合并转发时的详细消息列表
CREATE TABLE IF NOT EXISTS message_forward_details (
    detail_id BIGSERIAL PRIMARY KEY,
    forward_id BIGINT NOT NULL COMMENT '转发记录ID',
    source_message_id BIGINT NOT NULL COMMENT '源消息ID',
    sort_order INT DEFAULT 0 COMMENT '排序序号',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_forward_detail_forward FOREIGN KEY (forward_id) REFERENCES message_forwards(forward_id),
    CONSTRAINT fk_forward_detail_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX idx_forward_detail_forward ON message_forward_details(forward_id);
CREATE INDEX idx_forward_detail_message ON message_forward_details(source_message_id);

COMMENT ON TABLE message_forward_details IS '消息转发详情表';

-- =====================================================

-- 7. 语音转文字记录表
-- 用途：记录语音消息转文字的结果
CREATE TABLE IF NOT EXISTS voice_to_text_logs (
    log_id BIGSERIAL PRIMARY KEY,
    message_id BIGINT NOT NULL COMMENT '语音消息ID',
    account_id BIGINT NOT NULL COMMENT '所属账号ID',
    media_id BIGINT COMMENT '媒体文件ID',
    text_content TEXT COMMENT '转换后的文字内容',
    confidence DECIMAL(5, 4) COMMENT '识别置信度（0-1）',
    language VARCHAR(20) DEFAULT 'zh-CN' COMMENT '识别语言',
    duration INT COMMENT '语音时长（秒）',
    convert_status SMALLINT DEFAULT 1 COMMENT '转换状态：1-转换中 2-成功 3-失败',
    error_message VARCHAR(500) COMMENT '错误信息',
    converted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '转换时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_voice_message FOREIGN KEY (message_id) REFERENCES messages(message_id),
    CONSTRAINT fk_voice_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_voice_media FOREIGN KEY (media_id) REFERENCES message_media(media_id)
);

-- 创建索引
CREATE INDEX idx_voice_message ON voice_to_text_logs(message_id);
CREATE INDEX idx_voice_account ON voice_to_text_logs(account_id);
CREATE INDEX idx_voice_status ON voice_to_text_logs(convert_status);
CREATE INDEX idx_voice_time ON voice_to_text_logs(converted_at);

COMMENT ON TABLE voice_to_text_logs IS '语音转文字记录表';

-- =====================================================

-- 8. 群发消息记录表
-- 用途：记录群发消息任务及执行情况
CREATE TABLE IF NOT EXISTS mass_messages (
    mass_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '发送账号ID',
    message_type SMALLINT NOT NULL COMMENT '消息类型',
    content TEXT COMMENT '消息内容',
    target_count INT DEFAULT 0 COMMENT '目标数量',
    success_count INT DEFAULT 0 COMMENT '成功数量',
    fail_count INT DEFAULT 0 COMMENT '失败数量',
    mass_status SMALLINT DEFAULT 1 COMMENT '群发状态：1-待发送 2-发送中 3-已完成 4-已取消',
    scheduled_at TIMESTAMP COMMENT '计划发送时间',
    started_at TIMESTAMP COMMENT '开始发送时间',
    completed_at TIMESTAMP COMMENT '完成时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_mass_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_mass_account ON mass_messages(account_id);
CREATE INDEX idx_mass_status ON mass_messages(mass_status);
CREATE INDEX idx_mass_scheduled ON mass_messages(scheduled_at);
CREATE INDEX idx_mass_started ON mass_messages(started_at);

COMMENT ON TABLE mass_messages IS '群发消息记录表';

-- =====================================================

-- 9. 群发消息详情表
-- 用途：记录群发消息的每个目标的发送详情
CREATE TABLE IF NOT EXISTS mass_message_details (
    detail_id BIGSERIAL PRIMARY KEY,
    mass_id BIGINT NOT NULL COMMENT '群发任务ID',
    target_wxid VARCHAR(100) NOT NULL COMMENT '目标WXID',
    target_type SMALLINT COMMENT '目标类型：1-好友 2-群聊',
    message_id BIGINT COMMENT '实际发送的消息ID',
    send_status SMALLINT DEFAULT 1 COMMENT '发送状态：1-待发送 2-发送中 3-成功 4-失败',
    error_message VARCHAR(500) COMMENT '错误信息',
    sent_at TIMESTAMP COMMENT '发送时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_mass_detail_mass FOREIGN KEY (mass_id) REFERENCES mass_messages(mass_id),
    CONSTRAINT fk_mass_detail_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);

-- 创建索引
CREATE INDEX idx_mass_detail_mass ON mass_message_details(mass_id);
CREATE INDEX idx_mass_detail_target ON mass_message_details(target_wxid);
CREATE INDEX idx_mass_detail_status ON mass_message_details(send_status);
CREATE INDEX idx_mass_detail_message ON mass_message_details(message_id);

COMMENT ON TABLE mass_message_details IS '群发消息详情表';

-- =====================================================

-- 10. 消息同步日志表
-- 用途：记录消息同步任务（按时间段拉取历史消息）
CREATE TABLE IF NOT EXISTS message_sync_logs (
    log_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '账号ID',
    device_id BIGINT COMMENT '设备ID',
    sync_type SMALLINT DEFAULT 1 COMMENT '同步类型：1-全量同步 2-增量同步 3-指定时间段',
    start_time TIMESTAMP COMMENT '开始时间',
    end_time TIMESTAMP COMMENT '结束时间',
    total_count INT DEFAULT 0 COMMENT '总消息数',
    success_count INT DEFAULT 0 COMMENT '成功数',
    fail_count INT DEFAULT 0 COMMENT '失败数',
    sync_status SMALLINT DEFAULT 1 COMMENT '同步状态：1-同步中 2-已完成 3-失败 4-已取消',
    error_message TEXT COMMENT '错误信息',
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '开始时间',
    completed_at TIMESTAMP COMMENT '完成时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_sync_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_sync_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

-- 创建索引
CREATE INDEX idx_sync_account ON message_sync_logs(account_id);
CREATE INDEX idx_sync_device ON message_sync_logs(device_id);
CREATE INDEX idx_sync_status ON message_sync_logs(sync_status);
CREATE INDEX idx_sync_time ON message_sync_logs(start_time, end_time);

COMMENT ON TABLE message_sync_logs IS '消息同步日志表';

-- =====================================================
-- 创建更新时间触发器（复用已有函数）
-- =====================================================

CREATE TRIGGER trigger_messages_updated_at
    BEFORE UPDATE ON messages
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_message_media_updated_at
    BEFORE UPDATE ON message_media
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_message_extensions_updated_at
    BEFORE UPDATE ON message_extensions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_mass_messages_updated_at
    BEFORE UPDATE ON mass_messages
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 初始化数据（可选）
-- =====================================================

COMMENT ON COLUMN messages.chat_type IS '聊天类型：1-单聊 2-群聊 3-公众号消息 4-系统通知';
COMMENT ON COLUMN messages.message_type IS '消息类型：1-文本 2-图片 3-语音 4-视频 5-文件 6-名片 7-位置 8-链接 9-小程序 10-表情 11-系统消息 12-红包 13-转账 14-合并转发 15-接龙';
COMMENT ON COLUMN messages.direction IS '消息方向：1-发出 2-接收';
COMMENT ON COLUMN messages.send_status IS '发送状态：1-发送中 2-发送成功 3-发送失败 4-对方拒收';
COMMENT ON COLUMN messages.read_status IS '阅读状态：0-未读 1-已读 2-对方已读';

COMMENT ON COLUMN message_media.media_type IS '媒体类型：1-原图 2-缩略图 3-语音 4-视频 5-文件 6-视频封面';
COMMENT ON COLUMN message_media.download_status IS '下载状态：0-未下载 1-下载中 2-已下载 3-下载失败 4-已过期';
COMMENT ON COLUMN message_media.upload_status IS '上传状态：0-未上传 1-上传中 2-已上传到本地服务器 3-已上传到CDN 4-上传失败';

COMMENT ON COLUMN message_extensions.extension_type IS '扩展类型：1-名片 2-位置 3-文章链接 4-小程序 5-红包 6-转账 7-自定义表情 8-音乐分享 9-视频分享';

COMMENT ON COLUMN message_forwards.forward_type IS '转发类型：1-单条转发 2-合并转发（聊天记录） 3-逐条转发';
COMMENT ON COLUMN message_forwards.target_type IS '目标类型：1-好友单聊 2-群聊 3-文件传输助手';

COMMENT ON COLUMN mass_messages.mass_status IS '群发状态：1-待发送 2-发送中 3-已完成 4-部分失败 5-已取消';

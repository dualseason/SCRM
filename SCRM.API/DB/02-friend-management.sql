-- =====================================================
-- 模块：好友管理
-- 描述：用于管理微信好友关系、标签、加好友请求等信息
-- 创建时间：2025-11-25
-- =====================================================

-- 1. 联系人表（好友关系表）
-- 用途：存储微信账号的好友关系及好友信息
CREATE TABLE IF NOT EXISTS contacts (
    contact_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    friend_account_id BIGINT NOT NULL COMMENT '好友的微信账号ID',
    friend_wxid VARCHAR(100) NOT NULL COMMENT '好友微信WXID',
    friend_nickname VARCHAR(100) COMMENT '好友昵称',
    remark_name VARCHAR(100) COMMENT '备注名',
    remark_photo_url VARCHAR(500) COMMENT '备注图片URL',
    mobile_phone VARCHAR(20) COMMENT '手机号',
    description TEXT COMMENT '描述',
    source_type SMALLINT DEFAULT 0 COMMENT '来源类型：0-未知 1-搜索添加 2-扫码添加 3-群内添加 4-名片添加 5-通讯录添加 6-被动添加',
    source_info VARCHAR(200) COMMENT '来源信息（如：来自哪个群/谁的名片）',
    contact_type SMALLINT DEFAULT 1 COMMENT '联系人类型：1-好友 2-群聊 3-公众号 4-小程序',
    moments_permission SMALLINT DEFAULT 1 COMMENT '朋友圈权限：1-允许查看 2-不看他的朋友圈 3-不让他看我的朋友圈 4-互不看朋友圈',
    is_star BOOLEAN DEFAULT FALSE COMMENT '是否星标好友',
    is_blocked BOOLEAN DEFAULT FALSE COMMENT '是否拉黑',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否已删除好友',
    added_at TIMESTAMP COMMENT '添加时间',
    deleted_at TIMESTAMP COMMENT '删除时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_contact_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_contact_friend_account FOREIGN KEY (friend_account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT uk_contact_unique UNIQUE (account_id, friend_account_id)
);

-- 创建索引
CREATE INDEX idx_contacts_account ON contacts(account_id);
CREATE INDEX idx_contacts_friend_account ON contacts(friend_account_id);
CREATE INDEX idx_contacts_wxid ON contacts(friend_wxid);
CREATE INDEX idx_contacts_type ON contacts(contact_type, is_deleted);
CREATE INDEX idx_contacts_star ON contacts(is_star);
CREATE INDEX idx_contacts_blocked ON contacts(is_blocked);
CREATE INDEX idx_contacts_added_at ON contacts(added_at);

COMMENT ON TABLE contacts IS '联系人表（好友关系表）';

-- =====================================================

-- 2. 联系人标签表
-- 用途：定义联系人标签
CREATE TABLE IF NOT EXISTS contact_tags (
    tag_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    tag_name VARCHAR(50) NOT NULL COMMENT '标签名称',
    tag_color VARCHAR(20) COMMENT '标签颜色',
    tag_icon VARCHAR(100) COMMENT '标签图标',
    sort_order INT DEFAULT 0 COMMENT '排序序号',
    description VARCHAR(200) COMMENT '标签描述',
    is_system BOOLEAN DEFAULT FALSE COMMENT '是否系统标签',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
    deleted_at TIMESTAMP COMMENT '删除时间',

    CONSTRAINT fk_tag_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT uk_tag_name UNIQUE (account_id, tag_name, is_deleted)
);

-- 创建索引
CREATE INDEX idx_tags_account ON contact_tags(account_id);
CREATE INDEX idx_tags_name ON contact_tags(tag_name);
CREATE INDEX idx_tags_deleted ON contact_tags(is_deleted);

COMMENT ON TABLE contact_tags IS '联系人标签表';

-- =====================================================

-- 3. 联系人标签关系表
-- 用途：管理联系人与标签的多对多关系
CREATE TABLE IF NOT EXISTS contact_tag_relations (
    relation_id BIGSERIAL PRIMARY KEY,
    contact_id BIGINT NOT NULL COMMENT '联系人ID',
    tag_id BIGINT NOT NULL COMMENT '标签ID',
    tagged_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '打标签时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_tag_rel_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id),
    CONSTRAINT fk_tag_rel_tag FOREIGN KEY (tag_id) REFERENCES contact_tags(tag_id),
    CONSTRAINT uk_tag_rel_unique UNIQUE (contact_id, tag_id)
);

-- 创建索引
CREATE INDEX idx_tag_rel_contact ON contact_tag_relations(contact_id);
CREATE INDEX idx_tag_rel_tag ON contact_tag_relations(tag_id);
CREATE INDEX idx_tag_rel_time ON contact_tag_relations(tagged_at);

COMMENT ON TABLE contact_tag_relations IS '联系人标签关系表';

-- =====================================================

-- 4. 加好友请求表
-- 用途：记录加好友的申请和处理过程
CREATE TABLE IF NOT EXISTS friend_requests (
    request_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '发起请求的微信账号ID',
    target_wxid VARCHAR(100) NOT NULL COMMENT '目标微信WXID',
    target_account_id BIGINT COMMENT '目标微信账号ID（如果已存在）',
    request_type SMALLINT NOT NULL COMMENT '请求类型：1-主动添加 2-被动接受 3-群内添加 4-名片添加 5-通讯录添加 6-扫码添加',
    verify_message TEXT COMMENT '验证消息/申请理由',
    request_scene VARCHAR(100) COMMENT '申请场景（如群ID、名片来源等）',
    request_status SMALLINT DEFAULT 1 COMMENT '请求状态：1-待处理 2-已同意 3-已拒绝 4-已过期 5-已撤销',
    response_message VARCHAR(500) COMMENT '回复消息',
    requested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '请求时间',
    processed_at TIMESTAMP COMMENT '处理时间',
    expired_at TIMESTAMP COMMENT '过期时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',

    CONSTRAINT fk_friend_req_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_friend_req_target FOREIGN KEY (target_account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_friend_req_account ON friend_requests(account_id);
CREATE INDEX idx_friend_req_target_wxid ON friend_requests(target_wxid);
CREATE INDEX idx_friend_req_target_account ON friend_requests(target_account_id);
CREATE INDEX idx_friend_req_status ON friend_requests(request_status);
CREATE INDEX idx_friend_req_time ON friend_requests(requested_at);
CREATE INDEX idx_friend_req_processed ON friend_requests(processed_at);

COMMENT ON TABLE friend_requests IS '加好友请求表';

-- =====================================================

-- 5. 联系人变更日志表
-- 用途：记录联系人信息的变更历史
CREATE TABLE IF NOT EXISTS contact_change_logs (
    log_id BIGSERIAL PRIMARY KEY,
    contact_id BIGINT NOT NULL COMMENT '联系人ID',
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    change_type SMALLINT NOT NULL COMMENT '变更类型：1-添加好友 2-删除好友 3-修改备注 4-修改标签 5-拉黑 6-取消拉黑 7-设置星标 8-取消星标 9-朋友圈权限变更',
    field_name VARCHAR(50) COMMENT '变更字段名',
    old_value TEXT COMMENT '旧值',
    new_value TEXT COMMENT '新值',
    change_reason VARCHAR(200) COMMENT '变更原因',
    operator_type SMALLINT DEFAULT 1 COMMENT '操作者类型：1-用户操作 2-系统操作 3-批量操作',
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '变更时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_contact_log_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id),
    CONSTRAINT fk_contact_log_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);

-- 创建索引
CREATE INDEX idx_contact_log_contact ON contact_change_logs(contact_id);
CREATE INDEX idx_contact_log_account ON contact_change_logs(account_id);
CREATE INDEX idx_contact_log_type ON contact_change_logs(change_type);
CREATE INDEX idx_contact_log_time ON contact_change_logs(changed_at);

COMMENT ON TABLE contact_change_logs IS '联系人变更日志表';

-- =====================================================

-- 6. 联系人分组表
-- 用途：管理联系人的分组（如家人、同事、客户等）
CREATE TABLE IF NOT EXISTS contact_groups (
    group_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    group_name VARCHAR(50) NOT NULL COMMENT '分组名称',
    group_type SMALLINT DEFAULT 1 COMMENT '分组类型：1-自定义分组 2-系统分组',
    parent_group_id BIGINT COMMENT '父分组ID（支持层级分组）',
    sort_order INT DEFAULT 0 COMMENT '排序序号',
    description VARCHAR(200) COMMENT '分组描述',
    is_deleted BOOLEAN DEFAULT FALSE COMMENT '是否删除',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
    deleted_at TIMESTAMP COMMENT '删除时间',

    CONSTRAINT fk_group_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_group_parent FOREIGN KEY (parent_group_id) REFERENCES contact_groups(group_id),
    CONSTRAINT uk_group_name UNIQUE (account_id, group_name, is_deleted)
);

-- 创建索引
CREATE INDEX idx_groups_account ON contact_groups(account_id);
CREATE INDEX idx_groups_parent ON contact_groups(parent_group_id);
CREATE INDEX idx_groups_deleted ON contact_groups(is_deleted);

COMMENT ON TABLE contact_groups IS '联系人分组表';

-- =====================================================

-- 7. 联系人分组关系表
-- 用途：管理联系人与分组的多对多关系
CREATE TABLE IF NOT EXISTS contact_group_relations (
    relation_id BIGSERIAL PRIMARY KEY,
    contact_id BIGINT NOT NULL COMMENT '联系人ID',
    group_id BIGINT NOT NULL COMMENT '分组ID',
    grouped_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '加入分组时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_group_rel_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id),
    CONSTRAINT fk_group_rel_group FOREIGN KEY (group_id) REFERENCES contact_groups(group_id),
    CONSTRAINT uk_group_rel_unique UNIQUE (contact_id, group_id)
);

-- 创建索引
CREATE INDEX idx_group_rel_contact ON contact_group_relations(contact_id);
CREATE INDEX idx_group_rel_group ON contact_group_relations(group_id);
CREATE INDEX idx_group_rel_time ON contact_group_relations(grouped_at);

COMMENT ON TABLE contact_group_relations IS '联系人分组关系表';

-- =====================================================

-- 8. 好友检测记录表
-- 用途：记录僵尸粉检测任务及结果
CREATE TABLE IF NOT EXISTS friend_detection_logs (
    log_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL COMMENT '所属微信账号ID',
    contact_id BIGINT COMMENT '检测的联系人ID',
    detection_type SMALLINT DEFAULT 1 COMMENT '检测类型：1-单个检测 2-批量检测 3-全量检测',
    detection_result SMALLINT COMMENT '检测结果：1-正常 2-已删除 3-拉黑 4-异常',
    detection_method VARCHAR(50) COMMENT '检测方法',
    response_time INT COMMENT '响应时间（毫秒）',
    error_message VARCHAR(500) COMMENT '错误信息',
    detected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '检测时间',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

    CONSTRAINT fk_detection_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
    CONSTRAINT fk_detection_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id)
);

-- 创建索引
CREATE INDEX idx_detection_account ON friend_detection_logs(account_id);
CREATE INDEX idx_detection_contact ON friend_detection_logs(contact_id);
CREATE INDEX idx_detection_result ON friend_detection_logs(detection_result);
CREATE INDEX idx_detection_time ON friend_detection_logs(detected_at);

COMMENT ON TABLE friend_detection_logs IS '好友检测记录表';

-- =====================================================
-- 创建更新时间触发器（复用已有函数）
-- =====================================================

-- 为需要自动更新updated_at的表创建触发器
CREATE TRIGGER trigger_contacts_updated_at
    BEFORE UPDATE ON contacts
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_contact_tags_updated_at
    BEFORE UPDATE ON contact_tags
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_friend_requests_updated_at
    BEFORE UPDATE ON friend_requests
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER trigger_contact_groups_updated_at
    BEFORE UPDATE ON contact_groups
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 初始化数据（可选）
-- =====================================================

COMMENT ON COLUMN contacts.source_type IS '来源类型：1-搜索微信号添加 2-扫二维码添加 3-群内添加 4-名片推荐 5-通讯录导入 6-被动接受添加';
COMMENT ON COLUMN contacts.contact_type IS '联系人类型：1-个人好友 2-群聊 3-公众号 4-企业微信 5-小程序';
COMMENT ON COLUMN contacts.moments_permission IS '朋友圈权限：1-可以看我 2-不让他看我 3-不看他 4-互不可见';

COMMENT ON COLUMN friend_requests.request_type IS '请求类型：1-主动搜索添加 2-被动接受添加 3-群内添加 4-名片添加 5-通讯录添加 6-扫码添加';
COMMENT ON COLUMN friend_requests.request_status IS '请求状态：1-待对方确认 2-已同意 3-已拒绝 4-已过期 5-已撤销';

COMMENT ON COLUMN contact_change_logs.change_type IS '变更类型：1-添加好友 2-删除好友 3-修改备注名 4-添加标签 5-删除标签 6-拉黑 7-取消拉黑 8-设为星标 9-取消星标 10-修改朋友圈权限';

COMMENT ON COLUMN friend_detection_logs.detection_result IS '检测结果：1-好友正常 2-已被删除 3-已被拉黑 4-账号异常 5-检测失败';

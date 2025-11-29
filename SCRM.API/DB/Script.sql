-- DROP SCHEMA public;

CREATE SCHEMA public AUTHORIZATION pg_database_owner;

COMMENT ON SCHEMA public IS 'standard public schema';

-- DROP SEQUENCE account_status_logs_log_id_seq;

CREATE SEQUENCE account_status_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contact_change_logs_log_id_seq;

CREATE SEQUENCE contact_change_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contact_group_relations_relation_id_seq;

CREATE SEQUENCE contact_group_relations_relation_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contact_groups_group_id_seq;

CREATE SEQUENCE contact_groups_group_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contact_tag_relations_relation_id_seq;

CREATE SEQUENCE contact_tag_relations_relation_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contact_tags_tag_id_seq;

CREATE SEQUENCE contact_tags_tag_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE contacts_contact_id_seq;

CREATE SEQUENCE contacts_contact_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE device_authorizations_auth_id_seq;

CREATE SEQUENCE device_authorizations_auth_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE device_heartbeats_heartbeat_id_seq;

CREATE SEQUENCE device_heartbeats_heartbeat_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE device_version_logs_log_id_seq;

CREATE SEQUENCE device_version_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE devices_device_id_seq;

CREATE SEQUENCE devices_device_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE friend_detection_logs_log_id_seq;

CREATE SEQUENCE friend_detection_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE friend_requests_request_id_seq;

CREATE SEQUENCE friend_requests_request_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_announcements_announcement_id_seq;

CREATE SEQUENCE group_announcements_announcement_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_change_logs_log_id_seq;

CREATE SEQUENCE group_change_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_invitations_invitation_id_seq;

CREATE SEQUENCE group_invitations_invitation_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_members_member_id_seq;

CREATE SEQUENCE group_members_member_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_message_sync_logs_log_id_seq;

CREATE SEQUENCE group_message_sync_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE group_qrcodes_qrcode_id_seq;

CREATE SEQUENCE group_qrcodes_qrcode_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE groups_group_id_seq;

CREATE SEQUENCE groups_group_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE mass_message_details_detail_id_seq;

CREATE SEQUENCE mass_message_details_detail_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE mass_messages_mass_id_seq;

CREATE SEQUENCE mass_messages_mass_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_extensions_extension_id_seq;

CREATE SEQUENCE message_extensions_extension_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_forward_details_detail_id_seq;

CREATE SEQUENCE message_forward_details_detail_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_forwards_forward_id_seq;

CREATE SEQUENCE message_forwards_forward_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_media_media_id_seq;

CREATE SEQUENCE message_media_media_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_revocations_revocation_id_seq;

CREATE SEQUENCE message_revocations_revocation_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE message_sync_logs_log_id_seq;

CREATE SEQUENCE message_sync_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE messages_message_id_seq;

CREATE SEQUENCE messages_message_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE server_redirects_redirect_id_seq;

CREATE SEQUENCE server_redirects_redirect_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE voice_to_text_logs_log_id_seq;

CREATE SEQUENCE voice_to_text_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;
-- DROP SEQUENCE wechat_accounts_account_id_seq;

CREATE SEQUENCE wechat_accounts_account_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;-- public.devices definition

-- Drop table

-- DROP TABLE devices;

CREATE TABLE devices (
	device_id bigserial NOT NULL,
	device_uuid varchar(64) NOT NULL, -- 设备唯一标识（UUID/IMEI等）
	device_type int2 DEFAULT 1 NOT NULL, -- 设备类型：1-Android手机 2-iOS手机 3-Windows客服端 4-MacOS客服端 5-Web客服端
	device_name varchar(100) NULL, -- 设备名称
	os_type varchar(20) NULL, -- 操作系统类型：Android/iOS/Windows/MacOS
	os_version varchar(50) NULL, -- 操作系统版本
	device_model varchar(100) NULL, -- 设备型号
	device_brand varchar(50) NULL, -- 设备品牌
	sdk_version varchar(20) NULL, -- 当前SDK版本
	app_version varchar(20) NULL, -- APP版本
	is_active bool DEFAULT true NULL, -- 是否激活
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT devices_device_uuid_key UNIQUE (device_uuid),
	CONSTRAINT devices_pkey PRIMARY KEY (device_id)
);
CREATE INDEX idx_devices_active ON public.devices USING btree (is_active, is_deleted);
CREATE INDEX idx_devices_type ON public.devices USING btree (device_type);
CREATE INDEX idx_devices_uuid ON public.devices USING btree (device_uuid);
COMMENT ON TABLE public.devices IS '设备信息表';

-- Column comments

COMMENT ON COLUMN public.devices.device_uuid IS '设备唯一标识（UUID/IMEI等）';
COMMENT ON COLUMN public.devices.device_type IS '设备类型：1-Android手机 2-iOS手机 3-Windows客服端 4-MacOS客服端 5-Web客服端';
COMMENT ON COLUMN public.devices.device_name IS '设备名称';
COMMENT ON COLUMN public.devices.os_type IS '操作系统类型：Android/iOS/Windows/MacOS';
COMMENT ON COLUMN public.devices.os_version IS '操作系统版本';
COMMENT ON COLUMN public.devices.device_model IS '设备型号';
COMMENT ON COLUMN public.devices.device_brand IS '设备品牌';
COMMENT ON COLUMN public.devices.sdk_version IS '当前SDK版本';
COMMENT ON COLUMN public.devices.app_version IS 'APP版本';
COMMENT ON COLUMN public.devices.is_active IS '是否激活';
COMMENT ON COLUMN public.devices.is_deleted IS '是否删除';
COMMENT ON COLUMN public.devices.created_at IS '创建时间';
COMMENT ON COLUMN public.devices.updated_at IS '更新时间';
COMMENT ON COLUMN public.devices.deleted_at IS '删除时间';


-- public."groups" definition

-- Drop table

-- DROP TABLE "groups";

CREATE TABLE "groups" (
	group_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	group_wxid varchar(100) NOT NULL, -- 群聊WXID
	group_name varchar(255) NULL, -- 群名称
	group_notice varchar(500) NULL, -- 群公告
	group_avatar varchar(500) NULL, -- 群头像URL
	group_owner_wxid varchar(100) NULL, -- 群主微信WXID
	group_owner_id int8 NULL, -- 群主账号ID（如果是本系统用户）
	member_count int4 DEFAULT 0 NULL, -- 群成员数量
	max_member_count int4 DEFAULT 500 NULL, -- 群最大成员数
	group_status int2 DEFAULT 1 NULL, -- 群状态：1-正常 2-冻结 3-解散
	group_type int2 DEFAULT 1 NULL, -- 群类型：1-普通群 2-话题群
	is_announced bool DEFAULT false NULL, -- 是否有群公告
	is_frozen bool DEFAULT false NULL, -- 是否被冻结
	is_deleted bool DEFAULT false NULL, -- 是否已删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT groups_pkey PRIMARY KEY (group_id),
	CONSTRAINT uk_group_wxid UNIQUE (account_id, group_wxid)
);
CREATE INDEX idx_groups_member_count ON public.groups USING btree (member_count);
CREATE INDEX idx_groups_owner ON public.groups USING btree (group_owner_id);
CREATE INDEX idx_groups_status ON public.groups USING btree (group_status, is_deleted);
CREATE INDEX idx_groups_wxid ON public.groups USING btree (group_wxid);
COMMENT ON TABLE public."groups" IS '群聊表';

-- Column comments

COMMENT ON COLUMN public."groups".account_id IS '所属微信账号ID';
COMMENT ON COLUMN public."groups".group_wxid IS '群聊WXID';
COMMENT ON COLUMN public."groups".group_name IS '群名称';
COMMENT ON COLUMN public."groups".group_notice IS '群公告';
COMMENT ON COLUMN public."groups".group_avatar IS '群头像URL';
COMMENT ON COLUMN public."groups".group_owner_wxid IS '群主微信WXID';
COMMENT ON COLUMN public."groups".group_owner_id IS '群主账号ID（如果是本系统用户）';
COMMENT ON COLUMN public."groups".member_count IS '群成员数量';
COMMENT ON COLUMN public."groups".max_member_count IS '群最大成员数';
COMMENT ON COLUMN public."groups".group_status IS '群状态：1-正常 2-冻结 3-解散';
COMMENT ON COLUMN public."groups".group_type IS '群类型：1-普通群 2-话题群';
COMMENT ON COLUMN public."groups".is_announced IS '是否有群公告';
COMMENT ON COLUMN public."groups".is_frozen IS '是否被冻结';
COMMENT ON COLUMN public."groups".is_deleted IS '是否已删除';
COMMENT ON COLUMN public."groups".created_at IS '创建时间';
COMMENT ON COLUMN public."groups".updated_at IS '更新时间';
COMMENT ON COLUMN public."groups".deleted_at IS '删除时间';


-- public.wechat_accounts definition

-- Drop table

-- DROP TABLE wechat_accounts;

CREATE TABLE wechat_accounts (
	account_id bigserial NOT NULL,
	wxid varchar(100) NOT NULL, -- 微信WXID
	wechat_number varchar(50) NULL, -- 微信号
	nickname varchar(100) NULL, -- 微信昵称
	mobile_phone varchar(20) NULL, -- 手机号
	gender int2 DEFAULT 0 NULL, -- 性别：0-未知 1-男 2-女
	avatar_url varchar(500) NULL, -- 头像URL
	signature varchar(500) NULL, -- 个性签名
	qr_code_url varchar(500) NULL, -- 二维码URL
	region varchar(100) NULL, -- 地区
	account_status int2 DEFAULT 1 NULL, -- 账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常
	last_online_at timestamp NULL, -- 最后在线时间
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT wechat_accounts_pkey PRIMARY KEY (account_id),
	CONSTRAINT wechat_accounts_wxid_key UNIQUE (wxid)
);
CREATE INDEX idx_wechat_accounts_mobile ON public.wechat_accounts USING btree (mobile_phone);
CREATE INDEX idx_wechat_accounts_status ON public.wechat_accounts USING btree (account_status, is_deleted);
CREATE INDEX idx_wechat_accounts_wxid ON public.wechat_accounts USING btree (wxid);
COMMENT ON TABLE public.wechat_accounts IS '微信账号信息表';

-- Column comments

COMMENT ON COLUMN public.wechat_accounts.wxid IS '微信WXID';
COMMENT ON COLUMN public.wechat_accounts.wechat_number IS '微信号';
COMMENT ON COLUMN public.wechat_accounts.nickname IS '微信昵称';
COMMENT ON COLUMN public.wechat_accounts.mobile_phone IS '手机号';
COMMENT ON COLUMN public.wechat_accounts.gender IS '性别：0-未知 1-男 2-女';
COMMENT ON COLUMN public.wechat_accounts.avatar_url IS '头像URL';
COMMENT ON COLUMN public.wechat_accounts.signature IS '个性签名';
COMMENT ON COLUMN public.wechat_accounts.qr_code_url IS '二维码URL';
COMMENT ON COLUMN public.wechat_accounts.region IS '地区';
COMMENT ON COLUMN public.wechat_accounts.account_status IS '账号状态：1-正常在线 2-离线 3-冻结 4-注销 5-异常';
COMMENT ON COLUMN public.wechat_accounts.last_online_at IS '最后在线时间';
COMMENT ON COLUMN public.wechat_accounts.is_deleted IS '是否删除';
COMMENT ON COLUMN public.wechat_accounts.created_at IS '创建时间';
COMMENT ON COLUMN public.wechat_accounts.updated_at IS '更新时间';
COMMENT ON COLUMN public.wechat_accounts.deleted_at IS '删除时间';


-- public.contact_groups definition

-- Drop table

-- DROP TABLE contact_groups;

CREATE TABLE contact_groups (
	group_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	group_name varchar(50) NOT NULL, -- 分组名称
	group_type int2 DEFAULT 1 NULL, -- 分组类型：1-自定义分组 2-系统分组
	parent_group_id int8 NULL, -- 父分组ID（支持层级分组）
	sort_order int4 DEFAULT 0 NULL, -- 排序序号
	description varchar(200) NULL, -- 分组描述
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT contact_groups_pkey PRIMARY KEY (group_id),
	CONSTRAINT uk_group_name UNIQUE (account_id, group_name, is_deleted),
	CONSTRAINT fk_group_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_group_parent FOREIGN KEY (parent_group_id) REFERENCES contact_groups(group_id)
);
CREATE INDEX idx_groups_account ON public.contact_groups USING btree (account_id);
CREATE INDEX idx_groups_deleted ON public.contact_groups USING btree (is_deleted);
CREATE INDEX idx_groups_parent ON public.contact_groups USING btree (parent_group_id);
COMMENT ON TABLE public.contact_groups IS '联系人分组表';

-- Column comments

COMMENT ON COLUMN public.contact_groups.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.contact_groups.group_name IS '分组名称';
COMMENT ON COLUMN public.contact_groups.group_type IS '分组类型：1-自定义分组 2-系统分组';
COMMENT ON COLUMN public.contact_groups.parent_group_id IS '父分组ID（支持层级分组）';
COMMENT ON COLUMN public.contact_groups.sort_order IS '排序序号';
COMMENT ON COLUMN public.contact_groups.description IS '分组描述';
COMMENT ON COLUMN public.contact_groups.is_deleted IS '是否删除';
COMMENT ON COLUMN public.contact_groups.created_at IS '创建时间';
COMMENT ON COLUMN public.contact_groups.updated_at IS '更新时间';
COMMENT ON COLUMN public.contact_groups.deleted_at IS '删除时间';


-- public.contact_tags definition

-- Drop table

-- DROP TABLE contact_tags;

CREATE TABLE contact_tags (
	tag_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	tag_name varchar(50) NOT NULL, -- 标签名称
	tag_color varchar(20) NULL, -- 标签颜色
	tag_icon varchar(100) NULL, -- 标签图标
	sort_order int4 DEFAULT 0 NULL, -- 排序序号
	description varchar(200) NULL, -- 标签描述
	is_system bool DEFAULT false NULL, -- 是否系统标签
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT contact_tags_pkey PRIMARY KEY (tag_id),
	CONSTRAINT uk_tag_name UNIQUE (account_id, tag_name, is_deleted),
	CONSTRAINT fk_tag_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_tags_account ON public.contact_tags USING btree (account_id);
CREATE INDEX idx_tags_deleted ON public.contact_tags USING btree (is_deleted);
CREATE INDEX idx_tags_name ON public.contact_tags USING btree (tag_name);
COMMENT ON TABLE public.contact_tags IS '联系人标签表';

-- Column comments

COMMENT ON COLUMN public.contact_tags.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.contact_tags.tag_name IS '标签名称';
COMMENT ON COLUMN public.contact_tags.tag_color IS '标签颜色';
COMMENT ON COLUMN public.contact_tags.tag_icon IS '标签图标';
COMMENT ON COLUMN public.contact_tags.sort_order IS '排序序号';
COMMENT ON COLUMN public.contact_tags.description IS '标签描述';
COMMENT ON COLUMN public.contact_tags.is_system IS '是否系统标签';
COMMENT ON COLUMN public.contact_tags.is_deleted IS '是否删除';
COMMENT ON COLUMN public.contact_tags.created_at IS '创建时间';
COMMENT ON COLUMN public.contact_tags.updated_at IS '更新时间';
COMMENT ON COLUMN public.contact_tags.deleted_at IS '删除时间';


-- public.contacts definition

-- Drop table

-- DROP TABLE contacts;

CREATE TABLE contacts (
	contact_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	friend_account_id int8 NOT NULL, -- 好友的微信账号ID
	friend_wxid varchar(100) NOT NULL, -- 好友微信WXID
	friend_nickname varchar(100) NULL, -- 好友昵称
	remark_name varchar(100) NULL, -- 备注名
	remark_photo_url varchar(500) NULL, -- 备注图片URL
	mobile_phone varchar(20) NULL, -- 手机号
	description text NULL, -- 描述
	source_type int2 DEFAULT 0 NULL, -- 来源类型：1-搜索微信号添加 2-扫二维码添加 3-群内添加 4-名片推荐 5-通讯录导入 6-被动接受添加
	source_info varchar(200) NULL, -- 来源信息（如：来自哪个群/谁的名片）
	contact_type int2 DEFAULT 1 NULL, -- 联系人类型：1-个人好友 2-群聊 3-公众号 4-企业微信 5-小程序
	moments_permission int2 DEFAULT 1 NULL, -- 朋友圈权限：1-可以看我 2-不让他看我 3-不看他 4-互不可见
	is_star bool DEFAULT false NULL, -- 是否星标好友
	is_blocked bool DEFAULT false NULL, -- 是否拉黑
	is_deleted bool DEFAULT false NULL, -- 是否已删除好友
	added_at timestamp NULL, -- 添加时间
	deleted_at timestamp NULL, -- 删除时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT contacts_pkey PRIMARY KEY (contact_id),
	CONSTRAINT uk_contact_unique UNIQUE (account_id, friend_account_id),
	CONSTRAINT fk_contact_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_contact_friend_account FOREIGN KEY (friend_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_contacts_account ON public.contacts USING btree (account_id);
CREATE INDEX idx_contacts_added_at ON public.contacts USING btree (added_at);
CREATE INDEX idx_contacts_blocked ON public.contacts USING btree (is_blocked);
CREATE INDEX idx_contacts_friend_account ON public.contacts USING btree (friend_account_id);
CREATE INDEX idx_contacts_star ON public.contacts USING btree (is_star);
CREATE INDEX idx_contacts_type ON public.contacts USING btree (contact_type, is_deleted);
CREATE INDEX idx_contacts_wxid ON public.contacts USING btree (friend_wxid);
COMMENT ON TABLE public.contacts IS '联系人表（好友关系表）';

-- Column comments

COMMENT ON COLUMN public.contacts.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.contacts.friend_account_id IS '好友的微信账号ID';
COMMENT ON COLUMN public.contacts.friend_wxid IS '好友微信WXID';
COMMENT ON COLUMN public.contacts.friend_nickname IS '好友昵称';
COMMENT ON COLUMN public.contacts.remark_name IS '备注名';
COMMENT ON COLUMN public.contacts.remark_photo_url IS '备注图片URL';
COMMENT ON COLUMN public.contacts.mobile_phone IS '手机号';
COMMENT ON COLUMN public.contacts.description IS '描述';
COMMENT ON COLUMN public.contacts.source_type IS '来源类型：1-搜索微信号添加 2-扫二维码添加 3-群内添加 4-名片推荐 5-通讯录导入 6-被动接受添加';
COMMENT ON COLUMN public.contacts.source_info IS '来源信息（如：来自哪个群/谁的名片）';
COMMENT ON COLUMN public.contacts.contact_type IS '联系人类型：1-个人好友 2-群聊 3-公众号 4-企业微信 5-小程序';
COMMENT ON COLUMN public.contacts.moments_permission IS '朋友圈权限：1-可以看我 2-不让他看我 3-不看他 4-互不可见';
COMMENT ON COLUMN public.contacts.is_star IS '是否星标好友';
COMMENT ON COLUMN public.contacts.is_blocked IS '是否拉黑';
COMMENT ON COLUMN public.contacts.is_deleted IS '是否已删除好友';
COMMENT ON COLUMN public.contacts.added_at IS '添加时间';
COMMENT ON COLUMN public.contacts.deleted_at IS '删除时间';
COMMENT ON COLUMN public.contacts.created_at IS '创建时间';
COMMENT ON COLUMN public.contacts.updated_at IS '更新时间';


-- public.device_authorizations definition

-- Drop table

-- DROP TABLE device_authorizations;

CREATE TABLE device_authorizations (
	auth_id bigserial NOT NULL,
	device_id int8 NOT NULL, -- 设备ID
	account_id int8 NOT NULL, -- 微信账号ID
	access_token varchar(128) NOT NULL, -- 访问令牌
	token_expires_at timestamp NOT NULL, -- 令牌过期时间
	auth_status int2 DEFAULT 1 NULL, -- 授权状态：1-已授权在线 2-正常退出 3-强制下线 4-令牌过期 5-账号异常
	auth_time timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 授权时间
	logout_time timestamp NULL, -- 退出时间
	last_active_at timestamp NULL, -- 最后活跃时间
	client_ip varchar(50) NULL, -- 客户端IP地址
	server_ip varchar(50) NULL, -- 服务器IP地址
	server_port int4 NULL, -- 服务器端口
	logout_reason varchar(200) NULL, -- 退出原因
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT device_authorizations_access_token_key UNIQUE (access_token),
	CONSTRAINT device_authorizations_pkey PRIMARY KEY (auth_id),
	CONSTRAINT fk_device_auth_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_device_auth_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_device_auth_account ON public.device_authorizations USING btree (account_id);
CREATE INDEX idx_device_auth_active ON public.device_authorizations USING btree (last_active_at);
CREATE INDEX idx_device_auth_device ON public.device_authorizations USING btree (device_id);
CREATE INDEX idx_device_auth_status ON public.device_authorizations USING btree (auth_status, is_deleted);
CREATE INDEX idx_device_auth_token ON public.device_authorizations USING btree (access_token);
COMMENT ON TABLE public.device_authorizations IS '设备授权表';

-- Column comments

COMMENT ON COLUMN public.device_authorizations.device_id IS '设备ID';
COMMENT ON COLUMN public.device_authorizations.account_id IS '微信账号ID';
COMMENT ON COLUMN public.device_authorizations.access_token IS '访问令牌';
COMMENT ON COLUMN public.device_authorizations.token_expires_at IS '令牌过期时间';
COMMENT ON COLUMN public.device_authorizations.auth_status IS '授权状态：1-已授权在线 2-正常退出 3-强制下线 4-令牌过期 5-账号异常';
COMMENT ON COLUMN public.device_authorizations.auth_time IS '授权时间';
COMMENT ON COLUMN public.device_authorizations.logout_time IS '退出时间';
COMMENT ON COLUMN public.device_authorizations.last_active_at IS '最后活跃时间';
COMMENT ON COLUMN public.device_authorizations.client_ip IS '客户端IP地址';
COMMENT ON COLUMN public.device_authorizations.server_ip IS '服务器IP地址';
COMMENT ON COLUMN public.device_authorizations.server_port IS '服务器端口';
COMMENT ON COLUMN public.device_authorizations.logout_reason IS '退出原因';
COMMENT ON COLUMN public.device_authorizations.is_deleted IS '是否删除';
COMMENT ON COLUMN public.device_authorizations.created_at IS '创建时间';
COMMENT ON COLUMN public.device_authorizations.updated_at IS '更新时间';


-- public.device_heartbeats definition

-- Drop table

-- DROP TABLE device_heartbeats;

CREATE TABLE device_heartbeats (
	heartbeat_id bigserial NOT NULL,
	device_id int8 NOT NULL, -- 设备ID
	auth_id int8 NULL, -- 授权记录ID
	client_ip varchar(50) NULL, -- 客户端IP
	heartbeat_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 心跳时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT device_heartbeats_pkey PRIMARY KEY (heartbeat_id),
	CONSTRAINT fk_heartbeat_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id),
	CONSTRAINT fk_heartbeat_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_heartbeats_device ON public.device_heartbeats USING btree (device_id);
CREATE INDEX idx_heartbeats_device_time ON public.device_heartbeats USING btree (device_id, heartbeat_at DESC);
CREATE INDEX idx_heartbeats_time ON public.device_heartbeats USING btree (heartbeat_at);
COMMENT ON TABLE public.device_heartbeats IS '设备心跳记录表';

-- Column comments

COMMENT ON COLUMN public.device_heartbeats.device_id IS '设备ID';
COMMENT ON COLUMN public.device_heartbeats.auth_id IS '授权记录ID';
COMMENT ON COLUMN public.device_heartbeats.client_ip IS '客户端IP';
COMMENT ON COLUMN public.device_heartbeats.heartbeat_at IS '心跳时间';
COMMENT ON COLUMN public.device_heartbeats.created_at IS '创建时间';


-- public.device_version_logs definition

-- Drop table

-- DROP TABLE device_version_logs;

CREATE TABLE device_version_logs (
	log_id bigserial NOT NULL,
	device_id int8 NOT NULL, -- 设备ID
	sdk_version varchar(20) NOT NULL, -- SDK版本号
	app_version varchar(20) NULL, -- APP版本号
	os_version varchar(50) NULL, -- 操作系统版本
	reported_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 上报时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT device_version_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_version_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_version_logs_device ON public.device_version_logs USING btree (device_id);
CREATE INDEX idx_version_logs_time ON public.device_version_logs USING btree (reported_at);
COMMENT ON TABLE public.device_version_logs IS '设备版本记录表';

-- Column comments

COMMENT ON COLUMN public.device_version_logs.device_id IS '设备ID';
COMMENT ON COLUMN public.device_version_logs.sdk_version IS 'SDK版本号';
COMMENT ON COLUMN public.device_version_logs.app_version IS 'APP版本号';
COMMENT ON COLUMN public.device_version_logs.os_version IS '操作系统版本';
COMMENT ON COLUMN public.device_version_logs.reported_at IS '上报时间';
COMMENT ON COLUMN public.device_version_logs.created_at IS '创建时间';


-- public.friend_detection_logs definition

-- Drop table

-- DROP TABLE friend_detection_logs;

CREATE TABLE friend_detection_logs (
	log_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	contact_id int8 NULL, -- 检测的联系人ID
	detection_type int2 DEFAULT 1 NULL, -- 检测类型：1-单个检测 2-批量检测 3-全量检测
	detection_result int2 NULL, -- 检测结果：1-好友正常 2-已被删除 3-已被拉黑 4-账号异常 5-检测失败
	detection_method varchar(50) NULL, -- 检测方法
	response_time int4 NULL, -- 响应时间（毫秒）
	error_message varchar(500) NULL, -- 错误信息
	detected_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 检测时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT friend_detection_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_detection_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_detection_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id)
);
CREATE INDEX idx_detection_account ON public.friend_detection_logs USING btree (account_id);
CREATE INDEX idx_detection_contact ON public.friend_detection_logs USING btree (contact_id);
CREATE INDEX idx_detection_result ON public.friend_detection_logs USING btree (detection_result);
CREATE INDEX idx_detection_time ON public.friend_detection_logs USING btree (detected_at);
COMMENT ON TABLE public.friend_detection_logs IS '好友检测记录表';

-- Column comments

COMMENT ON COLUMN public.friend_detection_logs.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.friend_detection_logs.contact_id IS '检测的联系人ID';
COMMENT ON COLUMN public.friend_detection_logs.detection_type IS '检测类型：1-单个检测 2-批量检测 3-全量检测';
COMMENT ON COLUMN public.friend_detection_logs.detection_result IS '检测结果：1-好友正常 2-已被删除 3-已被拉黑 4-账号异常 5-检测失败';
COMMENT ON COLUMN public.friend_detection_logs.detection_method IS '检测方法';
COMMENT ON COLUMN public.friend_detection_logs.response_time IS '响应时间（毫秒）';
COMMENT ON COLUMN public.friend_detection_logs.error_message IS '错误信息';
COMMENT ON COLUMN public.friend_detection_logs.detected_at IS '检测时间';
COMMENT ON COLUMN public.friend_detection_logs.created_at IS '创建时间';


-- public.friend_requests definition

-- Drop table

-- DROP TABLE friend_requests;

CREATE TABLE friend_requests (
	request_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 发起请求的微信账号ID
	target_wxid varchar(100) NOT NULL, -- 目标微信WXID
	target_account_id int8 NULL, -- 目标微信账号ID（如果已存在）
	request_type int2 NOT NULL, -- 请求类型：1-主动搜索添加 2-被动接受添加 3-群内添加 4-名片添加 5-通讯录添加 6-扫码添加
	verify_message text NULL, -- 验证消息/申请理由
	request_scene varchar(100) NULL, -- 申请场景（如群ID、名片来源等）
	request_status int2 DEFAULT 1 NULL, -- 请求状态：1-待对方确认 2-已同意 3-已拒绝 4-已过期 5-已撤销
	response_message varchar(500) NULL, -- 回复消息
	requested_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 请求时间
	processed_at timestamp NULL, -- 处理时间
	expired_at timestamp NULL, -- 过期时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT friend_requests_pkey PRIMARY KEY (request_id),
	CONSTRAINT fk_friend_req_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_friend_req_target FOREIGN KEY (target_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_friend_req_account ON public.friend_requests USING btree (account_id);
CREATE INDEX idx_friend_req_processed ON public.friend_requests USING btree (processed_at);
CREATE INDEX idx_friend_req_status ON public.friend_requests USING btree (request_status);
CREATE INDEX idx_friend_req_target_account ON public.friend_requests USING btree (target_account_id);
CREATE INDEX idx_friend_req_target_wxid ON public.friend_requests USING btree (target_wxid);
CREATE INDEX idx_friend_req_time ON public.friend_requests USING btree (requested_at);
COMMENT ON TABLE public.friend_requests IS '加好友请求表';

-- Column comments

COMMENT ON COLUMN public.friend_requests.account_id IS '发起请求的微信账号ID';
COMMENT ON COLUMN public.friend_requests.target_wxid IS '目标微信WXID';
COMMENT ON COLUMN public.friend_requests.target_account_id IS '目标微信账号ID（如果已存在）';
COMMENT ON COLUMN public.friend_requests.request_type IS '请求类型：1-主动搜索添加 2-被动接受添加 3-群内添加 4-名片添加 5-通讯录添加 6-扫码添加';
COMMENT ON COLUMN public.friend_requests.verify_message IS '验证消息/申请理由';
COMMENT ON COLUMN public.friend_requests.request_scene IS '申请场景（如群ID、名片来源等）';
COMMENT ON COLUMN public.friend_requests.request_status IS '请求状态：1-待对方确认 2-已同意 3-已拒绝 4-已过期 5-已撤销';
COMMENT ON COLUMN public.friend_requests.response_message IS '回复消息';
COMMENT ON COLUMN public.friend_requests.requested_at IS '请求时间';
COMMENT ON COLUMN public.friend_requests.processed_at IS '处理时间';
COMMENT ON COLUMN public.friend_requests.expired_at IS '过期时间';
COMMENT ON COLUMN public.friend_requests.created_at IS '创建时间';
COMMENT ON COLUMN public.friend_requests.updated_at IS '更新时间';


-- public.group_announcements definition

-- Drop table

-- DROP TABLE group_announcements;

CREATE TABLE group_announcements (
	announcement_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	author_wxid varchar(100) NULL, -- 作者WXID
	title varchar(255) NULL, -- 公告标题
	"content" text NULL, -- 公告内容
	announcement_type int2 DEFAULT 1 NULL, -- 公告类型：1-普通公告 2-系统通知 3-重要通知
	is_pinned bool DEFAULT false NULL, -- 是否置顶
	is_active bool DEFAULT true NULL, -- 是否生效
	is_deleted bool DEFAULT false NULL, -- 是否已删除
	published_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 发布时间
	expires_at timestamp NULL, -- 过期时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT group_announcements_pkey PRIMARY KEY (announcement_id),
	CONSTRAINT fk_announcement_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_announcement_account ON public.group_announcements USING btree (account_id);
CREATE INDEX idx_announcement_group ON public.group_announcements USING btree (group_id);
CREATE INDEX idx_announcement_pinned ON public.group_announcements USING btree (is_pinned);
CREATE INDEX idx_announcement_time ON public.group_announcements USING btree (published_at);
CREATE INDEX idx_announcement_type ON public.group_announcements USING btree (announcement_type, is_deleted);
COMMENT ON TABLE public.group_announcements IS '群公告表';

-- Column comments

COMMENT ON COLUMN public.group_announcements.group_id IS '群ID';
COMMENT ON COLUMN public.group_announcements.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_announcements.author_wxid IS '作者WXID';
COMMENT ON COLUMN public.group_announcements.title IS '公告标题';
COMMENT ON COLUMN public.group_announcements."content" IS '公告内容';
COMMENT ON COLUMN public.group_announcements.announcement_type IS '公告类型：1-普通公告 2-系统通知 3-重要通知';
COMMENT ON COLUMN public.group_announcements.is_pinned IS '是否置顶';
COMMENT ON COLUMN public.group_announcements.is_active IS '是否生效';
COMMENT ON COLUMN public.group_announcements.is_deleted IS '是否已删除';
COMMENT ON COLUMN public.group_announcements.published_at IS '发布时间';
COMMENT ON COLUMN public.group_announcements.expires_at IS '过期时间';
COMMENT ON COLUMN public.group_announcements.created_at IS '创建时间';
COMMENT ON COLUMN public.group_announcements.updated_at IS '更新时间';


-- public.group_change_logs definition

-- Drop table

-- DROP TABLE group_change_logs;

CREATE TABLE group_change_logs (
	log_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	operator_wxid varchar(100) NULL, -- 操作者WXID
	change_type int2 NOT NULL, -- 变更类型：1-群创建 2-群解散 3-群名称变更 4-群公告变更 5-群头像变更 6-成员加入 7-成员退出 8-成员被移除 9-管理员变更 10-群主变更
	field_name varchar(50) NULL, -- 变更字段名
	old_value text NULL, -- 旧值
	new_value text NULL, -- 新值
	change_reason varchar(200) NULL, -- 变更原因
	operator_type int2 DEFAULT 1 NULL, -- 操作者类型：1-用户操作 2-系统操作 3-批量操作
	changed_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 变更时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT group_change_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_change_log_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_change_log_account ON public.group_change_logs USING btree (account_id);
CREATE INDEX idx_change_log_group ON public.group_change_logs USING btree (group_id);
CREATE INDEX idx_change_log_time ON public.group_change_logs USING btree (changed_at);
CREATE INDEX idx_change_log_type ON public.group_change_logs USING btree (change_type);
COMMENT ON TABLE public.group_change_logs IS '群变更日志表';

-- Column comments

COMMENT ON COLUMN public.group_change_logs.group_id IS '群ID';
COMMENT ON COLUMN public.group_change_logs.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_change_logs.operator_wxid IS '操作者WXID';
COMMENT ON COLUMN public.group_change_logs.change_type IS '变更类型：1-群创建 2-群解散 3-群名称变更 4-群公告变更 5-群头像变更 6-成员加入 7-成员退出 8-成员被移除 9-管理员变更 10-群主变更';
COMMENT ON COLUMN public.group_change_logs.field_name IS '变更字段名';
COMMENT ON COLUMN public.group_change_logs.old_value IS '旧值';
COMMENT ON COLUMN public.group_change_logs.new_value IS '新值';
COMMENT ON COLUMN public.group_change_logs.change_reason IS '变更原因';
COMMENT ON COLUMN public.group_change_logs.operator_type IS '操作者类型：1-用户操作 2-系统操作 3-批量操作';
COMMENT ON COLUMN public.group_change_logs.changed_at IS '变更时间';
COMMENT ON COLUMN public.group_change_logs.created_at IS '创建时间';


-- public.group_invitations definition

-- Drop table

-- DROP TABLE group_invitations;

CREATE TABLE group_invitations (
	invitation_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	inviter_wxid varchar(100) NULL, -- 邀请者WXID
	invitee_wxid varchar(100) NOT NULL, -- 被邀请者WXID
	invitee_account_id int8 NULL, -- 被邀请者账号ID（如果是本系统用户）
	invitation_type int2 DEFAULT 1 NULL, -- 邀请类型：1-群主邀请 2-管理员邀请 3-成员邀请
	invitation_status int2 DEFAULT 1 NULL, -- 邀请状态：1-待处理 2-已同意 3-已拒绝 4-已过期
	invitation_message varchar(500) NULL, -- 邀请消息
	response_message varchar(500) NULL, -- 回复消息
	invited_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 邀请时间
	responded_at timestamp NULL, -- 回复时间
	expires_at timestamp NULL, -- 过期时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT group_invitations_pkey PRIMARY KEY (invitation_id),
	CONSTRAINT fk_invitation_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_invitation_account ON public.group_invitations USING btree (account_id);
CREATE INDEX idx_invitation_group ON public.group_invitations USING btree (group_id);
CREATE INDEX idx_invitation_invitee ON public.group_invitations USING btree (invitee_account_id);
CREATE INDEX idx_invitation_status ON public.group_invitations USING btree (invitation_status);
CREATE INDEX idx_invitation_time ON public.group_invitations USING btree (invited_at);
CREATE INDEX idx_invitation_wxid ON public.group_invitations USING btree (invitee_wxid);
COMMENT ON TABLE public.group_invitations IS '群邀请请求表';

-- Column comments

COMMENT ON COLUMN public.group_invitations.group_id IS '群ID';
COMMENT ON COLUMN public.group_invitations.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_invitations.inviter_wxid IS '邀请者WXID';
COMMENT ON COLUMN public.group_invitations.invitee_wxid IS '被邀请者WXID';
COMMENT ON COLUMN public.group_invitations.invitee_account_id IS '被邀请者账号ID（如果是本系统用户）';
COMMENT ON COLUMN public.group_invitations.invitation_type IS '邀请类型：1-群主邀请 2-管理员邀请 3-成员邀请';
COMMENT ON COLUMN public.group_invitations.invitation_status IS '邀请状态：1-待处理 2-已同意 3-已拒绝 4-已过期';
COMMENT ON COLUMN public.group_invitations.invitation_message IS '邀请消息';
COMMENT ON COLUMN public.group_invitations.response_message IS '回复消息';
COMMENT ON COLUMN public.group_invitations.invited_at IS '邀请时间';
COMMENT ON COLUMN public.group_invitations.responded_at IS '回复时间';
COMMENT ON COLUMN public.group_invitations.expires_at IS '过期时间';
COMMENT ON COLUMN public.group_invitations.created_at IS '创建时间';


-- public.group_members definition

-- Drop table

-- DROP TABLE group_members;

CREATE TABLE group_members (
	member_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	member_wxid varchar(100) NOT NULL, -- 群成员WXID
	member_nickname varchar(100) NULL, -- 群内昵称
	member_avatar varchar(500) NULL, -- 群成员头像URL
	member_role int2 DEFAULT 1 NULL, -- 成员角色：1-普通成员 2-管理员 3-群主
	is_admin bool DEFAULT false NULL, -- 是否是管理员
	is_owner bool DEFAULT false NULL, -- 是否是群主
	joined_at timestamp NULL, -- 加入时间
	quit_at timestamp NULL, -- 退出时间
	is_active bool DEFAULT true NULL, -- 是否活跃
	is_deleted bool DEFAULT false NULL, -- 是否已删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT group_members_pkey PRIMARY KEY (member_id),
	CONSTRAINT uk_member_unique UNIQUE (group_id, member_wxid),
	CONSTRAINT fk_member_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_members_account ON public.group_members USING btree (account_id);
CREATE INDEX idx_members_active ON public.group_members USING btree (is_active, is_deleted);
CREATE INDEX idx_members_admin ON public.group_members USING btree (is_admin);
CREATE INDEX idx_members_group ON public.group_members USING btree (group_id);
CREATE INDEX idx_members_role ON public.group_members USING btree (member_role, is_deleted);
CREATE INDEX idx_members_wxid ON public.group_members USING btree (member_wxid);
COMMENT ON TABLE public.group_members IS '群成员表';

-- Column comments

COMMENT ON COLUMN public.group_members.group_id IS '群ID';
COMMENT ON COLUMN public.group_members.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_members.member_wxid IS '群成员WXID';
COMMENT ON COLUMN public.group_members.member_nickname IS '群内昵称';
COMMENT ON COLUMN public.group_members.member_avatar IS '群成员头像URL';
COMMENT ON COLUMN public.group_members.member_role IS '成员角色：1-普通成员 2-管理员 3-群主';
COMMENT ON COLUMN public.group_members.is_admin IS '是否是管理员';
COMMENT ON COLUMN public.group_members.is_owner IS '是否是群主';
COMMENT ON COLUMN public.group_members.joined_at IS '加入时间';
COMMENT ON COLUMN public.group_members.quit_at IS '退出时间';
COMMENT ON COLUMN public.group_members.is_active IS '是否活跃';
COMMENT ON COLUMN public.group_members.is_deleted IS '是否已删除';
COMMENT ON COLUMN public.group_members.created_at IS '创建时间';
COMMENT ON COLUMN public.group_members.updated_at IS '更新时间';


-- public.group_message_sync_logs definition

-- Drop table

-- DROP TABLE group_message_sync_logs;

CREATE TABLE group_message_sync_logs (
	log_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	device_id int8 NULL, -- 设备ID
	sync_type int2 DEFAULT 1 NULL, -- 同步类型：1-全量同步 2-增量同步 3-指定时间段
	start_time timestamp NULL, -- 开始时间
	end_time timestamp NULL, -- 结束时间
	total_count int4 DEFAULT 0 NULL, -- 总消息数
	success_count int4 DEFAULT 0 NULL, -- 成功数
	fail_count int4 DEFAULT 0 NULL, -- 失败数
	sync_status int2 DEFAULT 1 NULL, -- 同步状态：1-同步中 2-已完成 3-失败 4-已取消
	error_message text NULL, -- 错误信息
	started_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 开始时间
	completed_at timestamp NULL, -- 完成时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT group_message_sync_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_sync_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_sync_group ON public.group_message_sync_logs USING btree (group_id);
COMMENT ON TABLE public.group_message_sync_logs IS '群消息同步日志表';

-- Column comments

COMMENT ON COLUMN public.group_message_sync_logs.group_id IS '群ID';
COMMENT ON COLUMN public.group_message_sync_logs.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_message_sync_logs.device_id IS '设备ID';
COMMENT ON COLUMN public.group_message_sync_logs.sync_type IS '同步类型：1-全量同步 2-增量同步 3-指定时间段';
COMMENT ON COLUMN public.group_message_sync_logs.start_time IS '开始时间';
COMMENT ON COLUMN public.group_message_sync_logs.end_time IS '结束时间';
COMMENT ON COLUMN public.group_message_sync_logs.total_count IS '总消息数';
COMMENT ON COLUMN public.group_message_sync_logs.success_count IS '成功数';
COMMENT ON COLUMN public.group_message_sync_logs.fail_count IS '失败数';
COMMENT ON COLUMN public.group_message_sync_logs.sync_status IS '同步状态：1-同步中 2-已完成 3-失败 4-已取消';
COMMENT ON COLUMN public.group_message_sync_logs.error_message IS '错误信息';
COMMENT ON COLUMN public.group_message_sync_logs.started_at IS '开始时间';
COMMENT ON COLUMN public.group_message_sync_logs.completed_at IS '完成时间';
COMMENT ON COLUMN public.group_message_sync_logs.created_at IS '创建时间';


-- public.group_qrcodes definition

-- Drop table

-- DROP TABLE group_qrcodes;

CREATE TABLE group_qrcodes (
	qrcode_id bigserial NOT NULL,
	group_id int8 NOT NULL, -- 群ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	qrcode_url varchar(1000) NULL, -- 二维码URL
	qrcode_data text NULL, -- 二维码原始数据
	qrcode_type int2 DEFAULT 1 NULL, -- 二维码类型：1-邀请入群 2-分享群
	is_expired bool DEFAULT false NULL, -- 是否过期
	expires_at timestamp NULL, -- 过期时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT group_qrcodes_pkey PRIMARY KEY (qrcode_id),
	CONSTRAINT fk_qrcode_group FOREIGN KEY (group_id) REFERENCES "groups"(group_id)
);
CREATE INDEX idx_qrcode_account ON public.group_qrcodes USING btree (account_id);
CREATE INDEX idx_qrcode_expired ON public.group_qrcodes USING btree (is_expired);
CREATE INDEX idx_qrcode_group ON public.group_qrcodes USING btree (group_id);
CREATE INDEX idx_qrcode_time ON public.group_qrcodes USING btree (created_at);
COMMENT ON TABLE public.group_qrcodes IS '群二维码表';

-- Column comments

COMMENT ON COLUMN public.group_qrcodes.group_id IS '群ID';
COMMENT ON COLUMN public.group_qrcodes.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.group_qrcodes.qrcode_url IS '二维码URL';
COMMENT ON COLUMN public.group_qrcodes.qrcode_data IS '二维码原始数据';
COMMENT ON COLUMN public.group_qrcodes.qrcode_type IS '二维码类型：1-邀请入群 2-分享群';
COMMENT ON COLUMN public.group_qrcodes.is_expired IS '是否过期';
COMMENT ON COLUMN public.group_qrcodes.expires_at IS '过期时间';
COMMENT ON COLUMN public.group_qrcodes.created_at IS '创建时间';
COMMENT ON COLUMN public.group_qrcodes.updated_at IS '更新时间';


-- public.mass_messages definition

-- Drop table

-- DROP TABLE mass_messages;

CREATE TABLE mass_messages (
	mass_id bigserial NOT NULL,
	account_id int8 NOT NULL,
	message_type int2 NOT NULL,
	"content" text NULL,
	target_count int4 DEFAULT 0 NULL,
	success_count int4 DEFAULT 0 NULL,
	fail_count int4 DEFAULT 0 NULL,
	mass_status int2 DEFAULT 1 NULL,
	scheduled_at timestamp NULL,
	started_at timestamp NULL,
	completed_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT mass_messages_pkey PRIMARY KEY (mass_id),
	CONSTRAINT fk_mass_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_mass_account ON public.mass_messages USING btree (account_id);
CREATE INDEX idx_mass_scheduled ON public.mass_messages USING btree (scheduled_at);
CREATE INDEX idx_mass_started ON public.mass_messages USING btree (started_at);
CREATE INDEX idx_mass_status ON public.mass_messages USING btree (mass_status);
COMMENT ON TABLE public.mass_messages IS '群发消息记录表';


-- public.message_sync_logs definition

-- Drop table

-- DROP TABLE message_sync_logs;

CREATE TABLE message_sync_logs (
	log_id bigserial NOT NULL,
	account_id int8 NOT NULL,
	device_id int8 NULL,
	sync_type int2 DEFAULT 1 NULL,
	start_time timestamp NULL,
	end_time timestamp NULL,
	total_count int4 DEFAULT 0 NULL,
	success_count int4 DEFAULT 0 NULL,
	fail_count int4 DEFAULT 0 NULL,
	sync_status int2 DEFAULT 1 NULL,
	error_message text NULL,
	started_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	completed_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_sync_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_sync_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_sync_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_sync_account ON public.message_sync_logs USING btree (account_id);
CREATE INDEX idx_sync_device ON public.message_sync_logs USING btree (device_id);
CREATE INDEX idx_sync_status ON public.message_sync_logs USING btree (sync_status);
CREATE INDEX idx_sync_time ON public.message_sync_logs USING btree (start_time, end_time);
COMMENT ON TABLE public.message_sync_logs IS '消息同步日志表';


-- public.messages definition

-- Drop table

-- DROP TABLE messages;

CREATE TABLE messages (
	message_id bigserial NOT NULL,
	account_id int8 NOT NULL,
	msg_svr_id int8 NULL,
	conversation_id int8 NULL,
	sender_id int8 NULL,
	sender_wxid varchar(100) NULL,
	receiver_id int8 NULL,
	receiver_wxid varchar(100) NULL,
	chat_type int2 DEFAULT 1 NOT NULL,
	message_type int2 NOT NULL,
	"content" text NULL,
	content_xml text NULL,
	direction int2 NOT NULL,
	send_status int2 DEFAULT 1 NULL,
	read_status int2 DEFAULT 0 NULL,
	is_revoked bool DEFAULT false NULL,
	is_deleted bool DEFAULT false NULL,
	local_message_id varchar(100) NULL,
	client_msg_id varchar(100) NULL,
	sent_at timestamp NULL,
	received_at timestamp NULL,
	read_at timestamp NULL,
	revoked_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT messages_pkey PRIMARY KEY (message_id),
	CONSTRAINT fk_message_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_message_receiver FOREIGN KEY (receiver_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_message_sender FOREIGN KEY (sender_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_messages_account ON public.messages USING btree (account_id);
CREATE INDEX idx_messages_chat_type ON public.messages USING btree (chat_type);
CREATE INDEX idx_messages_conversation ON public.messages USING btree (conversation_id);
CREATE INDEX idx_messages_deleted ON public.messages USING btree (is_deleted);
CREATE INDEX idx_messages_direction ON public.messages USING btree (direction);
CREATE INDEX idx_messages_msg_svr_id ON public.messages USING btree (msg_svr_id);
CREATE INDEX idx_messages_received_at ON public.messages USING btree (received_at);
CREATE INDEX idx_messages_receiver ON public.messages USING btree (receiver_id);
CREATE INDEX idx_messages_receiver_wxid ON public.messages USING btree (receiver_wxid);
CREATE INDEX idx_messages_revoked ON public.messages USING btree (is_revoked);
CREATE INDEX idx_messages_sender ON public.messages USING btree (sender_id);
CREATE INDEX idx_messages_sender_wxid ON public.messages USING btree (sender_wxid);
CREATE INDEX idx_messages_sent_at ON public.messages USING btree (sent_at);
CREATE INDEX idx_messages_status ON public.messages USING btree (send_status, read_status);
CREATE INDEX idx_messages_type ON public.messages USING btree (message_type);
COMMENT ON TABLE public.messages IS '聊天消息表';


-- public.server_redirects definition

-- Drop table

-- DROP TABLE server_redirects;

CREATE TABLE server_redirects (
	redirect_id bigserial NOT NULL,
	device_id int8 NOT NULL, -- 设备ID
	auth_id int8 NULL, -- 授权记录ID
	old_server_ip varchar(50) NULL, -- 原服务器IP
	old_server_port int4 NULL, -- 原服务器端口
	new_server_ip varchar(50) NOT NULL, -- 新服务器IP
	new_server_port int4 NOT NULL, -- 新服务器端口
	redirect_reason varchar(200) NULL, -- 重定向原因
	redirect_type int2 DEFAULT 1 NULL, -- 重定向类型：1-负载均衡 2-服务器维护 3-异常切换
	redirected_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 重定向时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT server_redirects_pkey PRIMARY KEY (redirect_id),
	CONSTRAINT fk_redirect_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id),
	CONSTRAINT fk_redirect_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_redirects_device ON public.server_redirects USING btree (device_id);
CREATE INDEX idx_redirects_time ON public.server_redirects USING btree (redirected_at);
COMMENT ON TABLE public.server_redirects IS '服务器重定向记录表';

-- Column comments

COMMENT ON COLUMN public.server_redirects.device_id IS '设备ID';
COMMENT ON COLUMN public.server_redirects.auth_id IS '授权记录ID';
COMMENT ON COLUMN public.server_redirects.old_server_ip IS '原服务器IP';
COMMENT ON COLUMN public.server_redirects.old_server_port IS '原服务器端口';
COMMENT ON COLUMN public.server_redirects.new_server_ip IS '新服务器IP';
COMMENT ON COLUMN public.server_redirects.new_server_port IS '新服务器端口';
COMMENT ON COLUMN public.server_redirects.redirect_reason IS '重定向原因';
COMMENT ON COLUMN public.server_redirects.redirect_type IS '重定向类型：1-负载均衡 2-服务器维护 3-异常切换';
COMMENT ON COLUMN public.server_redirects.redirected_at IS '重定向时间';
COMMENT ON COLUMN public.server_redirects.created_at IS '创建时间';


-- public.account_status_logs definition

-- Drop table

-- DROP TABLE account_status_logs;

CREATE TABLE account_status_logs (
	log_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 微信账号ID
	device_id int8 NULL, -- 设备ID
	auth_id int8 NULL, -- 授权记录ID
	status_type int2 NOT NULL, -- 状态类型：1-上线 2-下线 3-主动登出 4-强制下线 5-令牌过期 6-网络断开
	previous_status int2 NULL, -- 之前状态
	current_status int2 NULL, -- 当前状态
	client_ip varchar(50) NULL, -- IP地址
	reason varchar(500) NULL, -- 原因/备注
	occurred_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 发生时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT account_status_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_status_log_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_status_log_auth FOREIGN KEY (auth_id) REFERENCES device_authorizations(auth_id),
	CONSTRAINT fk_status_log_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_status_logs_account ON public.account_status_logs USING btree (account_id);
CREATE INDEX idx_status_logs_device ON public.account_status_logs USING btree (device_id);
CREATE INDEX idx_status_logs_time ON public.account_status_logs USING btree (occurred_at);
CREATE INDEX idx_status_logs_type ON public.account_status_logs USING btree (status_type);
COMMENT ON TABLE public.account_status_logs IS '账号状态日志表';

-- Column comments

COMMENT ON COLUMN public.account_status_logs.account_id IS '微信账号ID';
COMMENT ON COLUMN public.account_status_logs.device_id IS '设备ID';
COMMENT ON COLUMN public.account_status_logs.auth_id IS '授权记录ID';
COMMENT ON COLUMN public.account_status_logs.status_type IS '状态类型：1-上线 2-下线 3-主动登出 4-强制下线 5-令牌过期 6-网络断开';
COMMENT ON COLUMN public.account_status_logs.previous_status IS '之前状态';
COMMENT ON COLUMN public.account_status_logs.current_status IS '当前状态';
COMMENT ON COLUMN public.account_status_logs.client_ip IS 'IP地址';
COMMENT ON COLUMN public.account_status_logs.reason IS '原因/备注';
COMMENT ON COLUMN public.account_status_logs.occurred_at IS '发生时间';
COMMENT ON COLUMN public.account_status_logs.created_at IS '创建时间';


-- public.contact_change_logs definition

-- Drop table

-- DROP TABLE contact_change_logs;

CREATE TABLE contact_change_logs (
	log_id bigserial NOT NULL,
	contact_id int8 NOT NULL, -- 联系人ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	change_type int2 NOT NULL, -- 变更类型：1-添加好友 2-删除好友 3-修改备注名 4-添加标签 5-删除标签 6-拉黑 7-取消拉黑 8-设为星标 9-取消星标 10-修改朋友圈权限
	field_name varchar(50) NULL, -- 变更字段名
	old_value text NULL, -- 旧值
	new_value text NULL, -- 新值
	change_reason varchar(200) NULL, -- 变更原因
	operator_type int2 DEFAULT 1 NULL, -- 操作者类型：1-用户操作 2-系统操作 3-批量操作
	changed_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 变更时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT contact_change_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_contact_log_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_contact_log_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id)
);
CREATE INDEX idx_contact_log_account ON public.contact_change_logs USING btree (account_id);
CREATE INDEX idx_contact_log_contact ON public.contact_change_logs USING btree (contact_id);
CREATE INDEX idx_contact_log_time ON public.contact_change_logs USING btree (changed_at);
CREATE INDEX idx_contact_log_type ON public.contact_change_logs USING btree (change_type);
COMMENT ON TABLE public.contact_change_logs IS '联系人变更日志表';

-- Column comments

COMMENT ON COLUMN public.contact_change_logs.contact_id IS '联系人ID';
COMMENT ON COLUMN public.contact_change_logs.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.contact_change_logs.change_type IS '变更类型：1-添加好友 2-删除好友 3-修改备注名 4-添加标签 5-删除标签 6-拉黑 7-取消拉黑 8-设为星标 9-取消星标 10-修改朋友圈权限';
COMMENT ON COLUMN public.contact_change_logs.field_name IS '变更字段名';
COMMENT ON COLUMN public.contact_change_logs.old_value IS '旧值';
COMMENT ON COLUMN public.contact_change_logs.new_value IS '新值';
COMMENT ON COLUMN public.contact_change_logs.change_reason IS '变更原因';
COMMENT ON COLUMN public.contact_change_logs.operator_type IS '操作者类型：1-用户操作 2-系统操作 3-批量操作';
COMMENT ON COLUMN public.contact_change_logs.changed_at IS '变更时间';
COMMENT ON COLUMN public.contact_change_logs.created_at IS '创建时间';


-- public.contact_group_relations definition

-- Drop table

-- DROP TABLE contact_group_relations;

CREATE TABLE contact_group_relations (
	relation_id bigserial NOT NULL,
	contact_id int8 NOT NULL, -- 联系人ID
	group_id int8 NOT NULL, -- 分组ID
	grouped_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 加入分组时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT contact_group_relations_pkey PRIMARY KEY (relation_id),
	CONSTRAINT uk_group_rel_unique UNIQUE (contact_id, group_id),
	CONSTRAINT fk_group_rel_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id),
	CONSTRAINT fk_group_rel_group FOREIGN KEY (group_id) REFERENCES contact_groups(group_id)
);
CREATE INDEX idx_group_rel_contact ON public.contact_group_relations USING btree (contact_id);
CREATE INDEX idx_group_rel_group ON public.contact_group_relations USING btree (group_id);
CREATE INDEX idx_group_rel_time ON public.contact_group_relations USING btree (grouped_at);
COMMENT ON TABLE public.contact_group_relations IS '联系人分组关系表';

-- Column comments

COMMENT ON COLUMN public.contact_group_relations.contact_id IS '联系人ID';
COMMENT ON COLUMN public.contact_group_relations.group_id IS '分组ID';
COMMENT ON COLUMN public.contact_group_relations.grouped_at IS '加入分组时间';
COMMENT ON COLUMN public.contact_group_relations.created_at IS '创建时间';


-- public.contact_tag_relations definition

-- Drop table

-- DROP TABLE contact_tag_relations;

CREATE TABLE contact_tag_relations (
	relation_id bigserial NOT NULL,
	contact_id int8 NOT NULL, -- 联系人ID
	tag_id int8 NOT NULL, -- 标签ID
	tagged_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 打标签时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT contact_tag_relations_pkey PRIMARY KEY (relation_id),
	CONSTRAINT uk_tag_rel_unique UNIQUE (contact_id, tag_id),
	CONSTRAINT fk_tag_rel_contact FOREIGN KEY (contact_id) REFERENCES contacts(contact_id),
	CONSTRAINT fk_tag_rel_tag FOREIGN KEY (tag_id) REFERENCES contact_tags(tag_id)
);
CREATE INDEX idx_tag_rel_contact ON public.contact_tag_relations USING btree (contact_id);
CREATE INDEX idx_tag_rel_tag ON public.contact_tag_relations USING btree (tag_id);
CREATE INDEX idx_tag_rel_time ON public.contact_tag_relations USING btree (tagged_at);
COMMENT ON TABLE public.contact_tag_relations IS '联系人标签关系表';

-- Column comments

COMMENT ON COLUMN public.contact_tag_relations.contact_id IS '联系人ID';
COMMENT ON COLUMN public.contact_tag_relations.tag_id IS '标签ID';
COMMENT ON COLUMN public.contact_tag_relations.tagged_at IS '打标签时间';
COMMENT ON COLUMN public.contact_tag_relations.created_at IS '创建时间';


-- public.mass_message_details definition

-- Drop table

-- DROP TABLE mass_message_details;

CREATE TABLE mass_message_details (
	detail_id bigserial NOT NULL,
	mass_id int8 NOT NULL,
	target_wxid varchar(100) NOT NULL,
	target_type int2 NULL,
	message_id int8 NULL,
	send_status int2 DEFAULT 1 NULL,
	error_message varchar(500) NULL,
	sent_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT mass_message_details_pkey PRIMARY KEY (detail_id),
	CONSTRAINT fk_mass_detail_mass FOREIGN KEY (mass_id) REFERENCES mass_messages(mass_id),
	CONSTRAINT fk_mass_detail_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_mass_detail_mass ON public.mass_message_details USING btree (mass_id);
CREATE INDEX idx_mass_detail_message ON public.mass_message_details USING btree (message_id);
CREATE INDEX idx_mass_detail_status ON public.mass_message_details USING btree (send_status);
CREATE INDEX idx_mass_detail_target ON public.mass_message_details USING btree (target_wxid);
COMMENT ON TABLE public.mass_message_details IS '群发消息详情表';


-- public.message_extensions definition

-- Drop table

-- DROP TABLE message_extensions;

CREATE TABLE message_extensions (
	extension_id bigserial NOT NULL,
	message_id int8 NOT NULL,
	extension_type int2 NOT NULL,
	title varchar(500) NULL,
	description text NULL,
	url varchar(1000) NULL,
	thumb_url varchar(1000) NULL,
	card_wxid varchar(100) NULL,
	card_nickname varchar(100) NULL,
	card_avatar varchar(500) NULL,
	latitude numeric(10, 7) NULL,
	longitude numeric(10, 7) NULL,
	location_label varchar(200) NULL,
	location_address varchar(500) NULL,
	appid varchar(100) NULL,
	page_path varchar(500) NULL,
	money_amount numeric(10, 2) NULL,
	money_type int2 NULL,
	money_status int2 NULL,
	emoji_md5 varchar(64) NULL,
	emoji_url varchar(1000) NULL,
	extra_data jsonb NULL,
	is_deleted bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_extensions_pkey PRIMARY KEY (extension_id),
	CONSTRAINT fk_extension_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_extension_appid ON public.message_extensions USING btree (appid);
CREATE INDEX idx_extension_card_wxid ON public.message_extensions USING btree (card_wxid);
CREATE INDEX idx_extension_message ON public.message_extensions USING btree (message_id);
CREATE INDEX idx_extension_type ON public.message_extensions USING btree (extension_type);
COMMENT ON TABLE public.message_extensions IS '消息扩展信息表';


-- public.message_forwards definition

-- Drop table

-- DROP TABLE message_forwards;

CREATE TABLE message_forwards (
	forward_id bigserial NOT NULL,
	account_id int8 NOT NULL,
	source_message_id int8 NULL,
	target_message_id int8 NULL,
	forward_type int2 NOT NULL,
	source_count int4 DEFAULT 1 NULL,
	target_wxid varchar(100) NULL,
	target_type int2 NULL,
	forward_status int2 DEFAULT 1 NULL,
	error_message varchar(500) NULL,
	forwarded_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_forwards_pkey PRIMARY KEY (forward_id),
	CONSTRAINT fk_forward_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_forward_source_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id),
	CONSTRAINT fk_forward_target_message FOREIGN KEY (target_message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_forward_account ON public.message_forwards USING btree (account_id);
CREATE INDEX idx_forward_source ON public.message_forwards USING btree (source_message_id);
CREATE INDEX idx_forward_status ON public.message_forwards USING btree (forward_status);
CREATE INDEX idx_forward_target ON public.message_forwards USING btree (target_message_id);
CREATE INDEX idx_forward_time ON public.message_forwards USING btree (forwarded_at);
CREATE INDEX idx_forward_type ON public.message_forwards USING btree (forward_type);
COMMENT ON TABLE public.message_forwards IS '消息转发记录表';


-- public.message_media definition

-- Drop table

-- DROP TABLE message_media;

CREATE TABLE message_media (
	media_id bigserial NOT NULL,
	message_id int8 NOT NULL,
	media_type int2 NOT NULL,
	file_name varchar(255) NULL,
	file_size int8 NULL,
	file_path varchar(500) NULL,
	file_url varchar(1000) NULL,
	cdn_url varchar(1000) NULL,
	mime_type varchar(100) NULL,
	file_md5 varchar(64) NULL,
	duration int4 NULL,
	width int4 NULL,
	height int4 NULL,
	thumbnail_url varchar(1000) NULL,
	download_status int2 DEFAULT 0 NULL,
	upload_status int2 DEFAULT 0 NULL,
	is_deleted bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_media_pkey PRIMARY KEY (media_id),
	CONSTRAINT fk_media_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_media_download ON public.message_media USING btree (download_status);
CREATE INDEX idx_media_md5 ON public.message_media USING btree (file_md5);
CREATE INDEX idx_media_message ON public.message_media USING btree (message_id);
CREATE INDEX idx_media_type ON public.message_media USING btree (media_type);
CREATE INDEX idx_media_upload ON public.message_media USING btree (upload_status);
COMMENT ON TABLE public.message_media IS '消息媒体文件表';


-- public.message_revocations definition

-- Drop table

-- DROP TABLE message_revocations;

CREATE TABLE message_revocations (
	revocation_id bigserial NOT NULL,
	message_id int8 NOT NULL,
	account_id int8 NOT NULL,
	operator_wxid varchar(100) NULL,
	revoke_type int2 DEFAULT 1 NULL,
	original_content text NULL,
	revoke_reason varchar(200) NULL,
	revoked_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_revocations_pkey PRIMARY KEY (revocation_id),
	CONSTRAINT fk_revocation_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_revocation_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_revocation_account ON public.message_revocations USING btree (account_id);
CREATE INDEX idx_revocation_message ON public.message_revocations USING btree (message_id);
CREATE INDEX idx_revocation_time ON public.message_revocations USING btree (revoked_at);
COMMENT ON TABLE public.message_revocations IS '消息撤回记录表';


-- public.voice_to_text_logs definition

-- Drop table

-- DROP TABLE voice_to_text_logs;

CREATE TABLE voice_to_text_logs (
	log_id bigserial NOT NULL,
	message_id int8 NOT NULL,
	account_id int8 NOT NULL,
	media_id int8 NULL,
	text_content text NULL,
	confidence numeric(5, 4) NULL,
	"language" varchar(20) DEFAULT 'zh-CN'::character varying NULL,
	duration int4 NULL,
	convert_status int2 DEFAULT 1 NULL,
	error_message varchar(500) NULL,
	converted_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT voice_to_text_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_voice_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_voice_media FOREIGN KEY (media_id) REFERENCES message_media(media_id),
	CONSTRAINT fk_voice_message FOREIGN KEY (message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_voice_account ON public.voice_to_text_logs USING btree (account_id);
CREATE INDEX idx_voice_message ON public.voice_to_text_logs USING btree (message_id);
CREATE INDEX idx_voice_status ON public.voice_to_text_logs USING btree (convert_status);
CREATE INDEX idx_voice_time ON public.voice_to_text_logs USING btree (converted_at);
COMMENT ON TABLE public.voice_to_text_logs IS '语音转文字记录表';


-- public.message_forward_details definition

-- Drop table

-- DROP TABLE message_forward_details;

CREATE TABLE message_forward_details (
	detail_id bigserial NOT NULL,
	forward_id int8 NOT NULL,
	source_message_id int8 NOT NULL,
	sort_order int4 DEFAULT 0 NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT message_forward_details_pkey PRIMARY KEY (detail_id),
	CONSTRAINT fk_forward_detail_forward FOREIGN KEY (forward_id) REFERENCES message_forwards(forward_id),
	CONSTRAINT fk_forward_detail_message FOREIGN KEY (source_message_id) REFERENCES messages(message_id)
);
CREATE INDEX idx_forward_detail_forward ON public.message_forward_details USING btree (forward_id);
CREATE INDEX idx_forward_detail_message ON public.message_forward_details USING btree (source_message_id);
COMMENT ON TABLE public.message_forward_details IS '消息转发详情表';



-- 五、朋友圈模块所需表

-- DROP SEQUENCE moments_posts_post_id_seq;

CREATE SEQUENCE moments_posts_post_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE moments_comments_comment_id_seq;

CREATE SEQUENCE moments_comments_comment_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE moments_likes_like_id_seq;

CREATE SEQUENCE moments_likes_like_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- public.moments_posts definition

-- DROP TABLE moments_posts;

CREATE TABLE moments_posts (
	post_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	content text NULL, -- 朋友圈文案
	content_type int2 DEFAULT 1 NULL, -- 内容类型：1-文字 2-图片 3-视频 4-链接 5-混合
	location varchar(200) NULL, -- 位置信息
	like_count int4 DEFAULT 0 NULL, -- 点赞数
	comment_count int4 DEFAULT 0 NULL, -- 评论数
	visibility int2 DEFAULT 1 NULL, -- 可见范围：1-所有人 2-仅朋友 3-部分可见 4-隐身
	is_top bool DEFAULT false NULL, -- 是否置顶
	is_deleted bool DEFAULT false NULL, -- 是否删除
	published_at timestamp NULL, -- 发布时间
	deleted_at timestamp NULL, -- 删除时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT moments_posts_pkey PRIMARY KEY (post_id),
	CONSTRAINT fk_post_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_moments_posts_account ON public.moments_posts USING btree (account_id);
CREATE INDEX idx_moments_posts_published ON public.moments_posts USING btree (published_at);
CREATE INDEX idx_moments_posts_deleted ON public.moments_posts USING btree (is_deleted);
CREATE INDEX idx_moments_posts_visibility ON public.moments_posts USING btree (visibility);
COMMENT ON TABLE public.moments_posts IS '朋友圈文章表';

-- Column comments

COMMENT ON COLUMN public.moments_posts.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.moments_posts.content IS '朋友圈文案';
COMMENT ON COLUMN public.moments_posts.content_type IS '内容类型：1-文字 2-图片 3-视频 4-链接 5-混合';
COMMENT ON COLUMN public.moments_posts.location IS '位置信息';
COMMENT ON COLUMN public.moments_posts.like_count IS '点赞数';
COMMENT ON COLUMN public.moments_posts.comment_count IS '评论数';
COMMENT ON COLUMN public.moments_posts.visibility IS '可见范围：1-所有人 2-仅朋友 3-部分可见 4-隐身';
COMMENT ON COLUMN public.moments_posts.is_top IS '是否置顶';
COMMENT ON COLUMN public.moments_posts.is_deleted IS '是否删除';
COMMENT ON COLUMN public.moments_posts.published_at IS '发布时间';
COMMENT ON COLUMN public.moments_posts.deleted_at IS '删除时间';
COMMENT ON COLUMN public.moments_posts.created_at IS '创建时间';
COMMENT ON COLUMN public.moments_posts.updated_at IS '更新时间';


-- public.moments_likes definition

-- DROP TABLE moments_likes;

CREATE TABLE moments_likes (
	like_id bigserial NOT NULL,
	post_id int8 NOT NULL, -- 朋友圈ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	liked_by_wxid varchar(100) NOT NULL, -- 点赞者WXID
	liked_by_account_id int8 NULL, -- 点赞者账号ID（如果是本系统用户）
	like_type int2 DEFAULT 1 NULL, -- 点赞类型：1-赞 2-emoji点赞
	emoji_id varchar(100) NULL, -- emoji表情ID
	liked_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 点赞时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT moments_likes_pkey PRIMARY KEY (like_id),
	CONSTRAINT uk_post_like UNIQUE (post_id, liked_by_wxid),
	CONSTRAINT fk_like_post FOREIGN KEY (post_id) REFERENCES moments_posts(post_id),
	CONSTRAINT fk_like_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_like_by_account FOREIGN KEY (liked_by_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_moments_likes_post ON public.moments_likes USING btree (post_id);
CREATE INDEX idx_moments_likes_account ON public.moments_likes USING btree (account_id);
CREATE INDEX idx_moments_likes_liked_by ON public.moments_likes USING btree (liked_by_account_id);
CREATE INDEX idx_moments_likes_time ON public.moments_likes USING btree (liked_at);
COMMENT ON TABLE public.moments_likes IS '朋友圈点赞表';

-- Column comments

COMMENT ON COLUMN public.moments_likes.post_id IS '朋友圈ID';
COMMENT ON COLUMN public.moments_likes.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.moments_likes.liked_by_wxid IS '点赞者WXID';
COMMENT ON COLUMN public.moments_likes.liked_by_account_id IS '点赞者账号ID（如果是本系统用户）';
COMMENT ON COLUMN public.moments_likes.like_type IS '点赞类型：1-赞 2-emoji点赞';
COMMENT ON COLUMN public.moments_likes.emoji_id IS 'emoji表情ID';
COMMENT ON COLUMN public.moments_likes.liked_at IS '点赞时间';
COMMENT ON COLUMN public.moments_likes.created_at IS '创建时间';


-- public.moments_comments definition

-- DROP TABLE moments_comments;

CREATE TABLE moments_comments (
	comment_id bigserial NOT NULL,
	post_id int8 NOT NULL, -- 朋友圈ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	comment_by_wxid varchar(100) NOT NULL, -- 评论者WXID
	comment_by_account_id int8 NULL, -- 评论者账号ID（如果是本系统用户）
	reply_to_comment_id int8 NULL, -- 回复的评论ID
	reply_to_wxid varchar(100) NULL, -- 回复的对象WXID
	comment_content text NOT NULL, -- 评论内容
	is_deleted bool DEFAULT false NULL, -- 是否删除
	commented_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 评论时间
	deleted_at timestamp NULL, -- 删除时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT moments_comments_pkey PRIMARY KEY (comment_id),
	CONSTRAINT fk_comment_post FOREIGN KEY (post_id) REFERENCES moments_posts(post_id),
	CONSTRAINT fk_comment_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_comment_by_account FOREIGN KEY (comment_by_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_comment_reply FOREIGN KEY (reply_to_comment_id) REFERENCES moments_comments(comment_id)
);
CREATE INDEX idx_moments_comments_post ON public.moments_comments USING btree (post_id);
CREATE INDEX idx_moments_comments_account ON public.moments_comments USING btree (account_id);
CREATE INDEX idx_moments_comments_by ON public.moments_comments USING btree (comment_by_account_id);
CREATE INDEX idx_moments_comments_time ON public.moments_comments USING btree (commented_at);
CREATE INDEX idx_moments_comments_deleted ON public.moments_comments USING btree (is_deleted);
COMMENT ON TABLE public.moments_comments IS '朋友圈评论表';

-- Column comments

COMMENT ON COLUMN public.moments_comments.post_id IS '朋友圈ID';
COMMENT ON COLUMN public.moments_comments.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.moments_comments.comment_by_wxid IS '评论者WXID';
COMMENT ON COLUMN public.moments_comments.comment_by_account_id IS '评论者账号ID（如果是本系统用户）';
COMMENT ON COLUMN public.moments_comments.reply_to_comment_id IS '回复的评论ID';
COMMENT ON COLUMN public.moments_comments.reply_to_wxid IS '回复的对象WXID';
COMMENT ON COLUMN public.moments_comments.comment_content IS '评论内容';
COMMENT ON COLUMN public.moments_comments.is_deleted IS '是否删除';
COMMENT ON COLUMN public.moments_comments.commented_at IS '评论时间';
COMMENT ON COLUMN public.moments_comments.deleted_at IS '删除时间';
COMMENT ON COLUMN public.moments_comments.created_at IS '创建时间';
COMMENT ON COLUMN public.moments_comments.updated_at IS '更新时间';


-- 六、钱包与红包模块所需表

-- DROP SEQUENCE wallet_transactions_transaction_id_seq;

CREATE SEQUENCE wallet_transactions_transaction_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE red_packets_packet_id_seq;

CREATE SEQUENCE red_packets_packet_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE red_packet_records_record_id_seq;

CREATE SEQUENCE red_packet_records_record_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- public.wallet_transactions definition

-- DROP TABLE wallet_transactions;

CREATE TABLE wallet_transactions (
	transaction_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	transaction_type int2 NOT NULL, -- 交易类型：1-收入 2-支出 3-转账 4-红包
	related_account_id int8 NULL, -- 相关账号ID
	related_wxid varchar(100) NULL, -- 相关微信WXID
	amount numeric(15, 2) NOT NULL, -- 交易金额
	balance_before numeric(15, 2) NULL, -- 交易前余额
	balance_after numeric(15, 2) NULL, -- 交易后余额
	description varchar(500) NULL, -- 交易描述
	transaction_status int2 DEFAULT 1 NULL, -- 交易状态：1-待确认 2-已确认 3-失败 4-已撤销
	transaction_at timestamp NULL, -- 交易时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT wallet_transactions_pkey PRIMARY KEY (transaction_id),
	CONSTRAINT fk_wallet_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_wallet_related_account FOREIGN KEY (related_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_wallet_account ON public.wallet_transactions USING btree (account_id);
CREATE INDEX idx_wallet_type ON public.wallet_transactions USING btree (transaction_type);
CREATE INDEX idx_wallet_status ON public.wallet_transactions USING btree (transaction_status);
CREATE INDEX idx_wallet_time ON public.wallet_transactions USING btree (transaction_at);
CREATE INDEX idx_wallet_related_account ON public.wallet_transactions USING btree (related_account_id);
COMMENT ON TABLE public.wallet_transactions IS '钱包交易记录表';

-- Column comments

COMMENT ON COLUMN public.wallet_transactions.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.wallet_transactions.transaction_type IS '交易类型：1-收入 2-支出 3-转账 4-红包';
COMMENT ON COLUMN public.wallet_transactions.related_account_id IS '相关账号ID';
COMMENT ON COLUMN public.wallet_transactions.related_wxid IS '相关微信WXID';
COMMENT ON COLUMN public.wallet_transactions.amount IS '交易金额';
COMMENT ON COLUMN public.wallet_transactions.balance_before IS '交易前余额';
COMMENT ON COLUMN public.wallet_transactions.balance_after IS '交易后余额';
COMMENT ON COLUMN public.wallet_transactions.description IS '交易描述';
COMMENT ON COLUMN public.wallet_transactions.transaction_status IS '交易状态：1-待确认 2-已确认 3-失败 4-已撤销';
COMMENT ON COLUMN public.wallet_transactions.transaction_at IS '交易时间';
COMMENT ON COLUMN public.wallet_transactions.created_at IS '创建时间';
COMMENT ON COLUMN public.wallet_transactions.updated_at IS '更新时间';


-- public.red_packets definition

-- DROP TABLE red_packets;

CREATE TABLE red_packets (
	packet_id bigserial NOT NULL,
	account_id int8 NOT NULL, -- 所属微信账号ID
	target_wxid varchar(100) NULL, -- 目标WXID（单人或群）
	target_type int2 NOT NULL, -- 目标类型：1-个人 2-群聊
	total_amount numeric(15, 2) NOT NULL, -- 总金额
	packet_count int4 NOT NULL, -- 红包个数
	distributed_count int4 DEFAULT 0 NULL, -- 已领取个数
	distributed_amount numeric(15, 2) DEFAULT 0 NULL, -- 已领取金额
	greeting_message varchar(500) NULL, -- 贺词
	packet_type int2 DEFAULT 1 NULL, -- 红包类型：1-普通红包 2-拼手气红包 3-定额红包
	packet_status int2 DEFAULT 1 NULL, -- 红包状态：1-待发送 2-已发送 3-已领完 4-已过期 5-已撤销
	expires_at timestamp NULL, -- 过期时间
	sent_at timestamp NULL, -- 发送时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT red_packets_pkey PRIMARY KEY (packet_id),
	CONSTRAINT fk_red_packet_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_red_packets_account ON public.red_packets USING btree (account_id);
CREATE INDEX idx_red_packets_target ON public.red_packets USING btree (target_wxid);
CREATE INDEX idx_red_packets_status ON public.red_packets USING btree (packet_status);
CREATE INDEX idx_red_packets_time ON public.red_packets USING btree (sent_at);
CREATE INDEX idx_red_packets_expires ON public.red_packets USING btree (expires_at);
COMMENT ON TABLE public.red_packets IS '红包发送记录表';

-- Column comments

COMMENT ON COLUMN public.red_packets.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.red_packets.target_wxid IS '目标WXID（单人或群）';
COMMENT ON COLUMN public.red_packets.target_type IS '目标类型：1-个人 2-群聊';
COMMENT ON COLUMN public.red_packets.total_amount IS '总金额';
COMMENT ON COLUMN public.red_packets.packet_count IS '红包个数';
COMMENT ON COLUMN public.red_packets.distributed_count IS '已领取个数';
COMMENT ON COLUMN public.red_packets.distributed_amount IS '已领取金额';
COMMENT ON COLUMN public.red_packets.greeting_message IS '贺词';
COMMENT ON COLUMN public.red_packets.packet_type IS '红包类型：1-普通红包 2-拼手气红包 3-定额红包';
COMMENT ON COLUMN public.red_packets.packet_status IS '红包状态：1-待发送 2-已发送 3-已领完 4-已过期 5-已撤销';
COMMENT ON COLUMN public.red_packets.expires_at IS '过期时间';
COMMENT ON COLUMN public.red_packets.sent_at IS '发送时间';
COMMENT ON COLUMN public.red_packets.created_at IS '创建时间';
COMMENT ON COLUMN public.red_packets.updated_at IS '更新时间';


-- public.red_packet_records definition

-- DROP TABLE red_packet_records;

CREATE TABLE red_packet_records (
	record_id bigserial NOT NULL,
	packet_id int8 NOT NULL, -- 红包ID
	account_id int8 NOT NULL, -- 所属微信账号ID
	received_by_wxid varchar(100) NOT NULL, -- 领取者WXID
	received_by_account_id int8 NULL, -- 领取者账号ID（如果是本系统用户）
	amount numeric(15, 2) NOT NULL, -- 领取金额
	received_at timestamp NULL, -- 领取时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT red_packet_records_pkey PRIMARY KEY (record_id),
	CONSTRAINT fk_record_packet FOREIGN KEY (packet_id) REFERENCES red_packets(packet_id),
	CONSTRAINT fk_record_account FOREIGN KEY (account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_record_received_account FOREIGN KEY (received_by_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_red_packet_records_packet ON public.red_packet_records USING btree (packet_id);
CREATE INDEX idx_red_packet_records_account ON public.red_packet_records USING btree (account_id);
CREATE INDEX idx_red_packet_records_received ON public.red_packet_records USING btree (received_by_account_id);
CREATE INDEX idx_red_packet_records_time ON public.red_packet_records USING btree (received_at);
COMMENT ON TABLE public.red_packet_records IS '红包领取记录表';

-- Column comments

COMMENT ON COLUMN public.red_packet_records.packet_id IS '红包ID';
COMMENT ON COLUMN public.red_packet_records.account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.red_packet_records.received_by_wxid IS '领取者WXID';
COMMENT ON COLUMN public.red_packet_records.received_by_account_id IS '领取者账号ID（如果是本系统用户）';
COMMENT ON COLUMN public.red_packet_records.amount IS '领取金额';
COMMENT ON COLUMN public.red_packet_records.received_at IS '领取时间';
COMMENT ON COLUMN public.red_packet_records.created_at IS '创建时间';


-- 七、公众号与小程序模块所需表

-- DROP SEQUENCE official_accounts_account_id_seq;

CREATE SEQUENCE official_accounts_account_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE miniprogram_accounts_account_id_seq;

CREATE SEQUENCE miniprogram_accounts_account_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE official_account_search_logs_log_id_seq;

CREATE SEQUENCE official_account_search_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE miniprogram_search_logs_log_id_seq;

CREATE SEQUENCE miniprogram_search_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE official_account_messages_message_id_seq;

CREATE SEQUENCE official_account_messages_message_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE miniprogram_messages_message_id_seq;

CREATE SEQUENCE miniprogram_messages_message_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE official_account_subscriptions_subscription_id_seq;

CREATE SEQUENCE official_account_subscriptions_subscription_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE miniprogram_access_logs_log_id_seq;

CREATE SEQUENCE miniprogram_access_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE official_account_follow_logs_log_id_seq;

CREATE SEQUENCE official_account_follow_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- DROP SEQUENCE miniprogram_follow_logs_log_id_seq;

CREATE SEQUENCE miniprogram_follow_logs_log_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- public.official_accounts definition

-- DROP TABLE official_accounts;

CREATE TABLE official_accounts (
	account_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 关联的微信账号ID
	official_account_name varchar(255) NOT NULL, -- 公众号名称
	official_wxid varchar(100) NOT NULL, -- 公众号微信ID
	official_account_type int2 DEFAULT 1 NULL, -- 公众号类型：1-订阅号 2-服务号 3-企业号
	official_qrcode varchar(500) NULL, -- 公众号二维码
	official_avatar varchar(500) NULL, -- 公众号头像
	official_description text NULL, -- 公众号描述
	official_url varchar(500) NULL, -- 公众号链接
	follow_status int2 DEFAULT 1 NULL, -- 关注状态：1-已关注 2-未关注 3-已取消关注
	follow_at timestamp NULL, -- 关注时间
	unfollow_at timestamp NULL, -- 取消关注时间
	last_msg_at timestamp NULL, -- 最后消息时间
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT official_accounts_pkey PRIMARY KEY (account_id),
	CONSTRAINT uk_official_wxid UNIQUE (wechat_account_id, official_wxid),
	CONSTRAINT fk_official_account FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_official_accounts_wechat ON public.official_accounts USING btree (wechat_account_id);
CREATE INDEX idx_official_accounts_type ON public.official_accounts USING btree (official_account_type);
CREATE INDEX idx_official_accounts_follow ON public.official_accounts USING btree (follow_status, is_deleted);
CREATE INDEX idx_official_accounts_time ON public.official_accounts USING btree (follow_at, unfollow_at);
COMMENT ON TABLE public.official_accounts IS '公众号账号表';

-- Column comments

COMMENT ON COLUMN public.official_accounts.wechat_account_id IS '关联的微信账号ID';
COMMENT ON COLUMN public.official_accounts.official_account_name IS '公众号名称';
COMMENT ON COLUMN public.official_accounts.official_wxid IS '公众号微信ID';
COMMENT ON COLUMN public.official_accounts.official_account_type IS '公众号类型：1-订阅号 2-服务号 3-企业号';
COMMENT ON COLUMN public.official_accounts.official_qrcode IS '公众号二维码';
COMMENT ON COLUMN public.official_accounts.official_avatar IS '公众号头像';
COMMENT ON COLUMN public.official_accounts.official_description IS '公众号描述';
COMMENT ON COLUMN public.official_accounts.official_url IS '公众号链接';
COMMENT ON COLUMN public.official_accounts.follow_status IS '关注状态：1-已关注 2-未关注 3-已取消关注';
COMMENT ON COLUMN public.official_accounts.follow_at IS '关注时间';
COMMENT ON COLUMN public.official_accounts.unfollow_at IS '取消关注时间';
COMMENT ON COLUMN public.official_accounts.last_msg_at IS '最后消息时间';
COMMENT ON COLUMN public.official_accounts.is_deleted IS '是否删除';
COMMENT ON COLUMN public.official_accounts.created_at IS '创建时间';
COMMENT ON COLUMN public.official_accounts.updated_at IS '更新时间';
COMMENT ON COLUMN public.official_accounts.deleted_at IS '删除时间';


-- public.miniprogram_accounts definition

-- DROP TABLE miniprogram_accounts;

CREATE TABLE miniprogram_accounts (
	account_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 关联的微信账号ID
	miniprogram_name varchar(255) NOT NULL, -- 小程序名称
	miniprogram_wxid varchar(100) NOT NULL, -- 小程序微信ID
	miniprogram_appid varchar(100) NULL, -- 小程序AppID
	miniprogram_avatar varchar(500) NULL, -- 小程序头像
	miniprogram_description text NULL, -- 小程序描述
	miniprogram_category varchar(100) NULL, -- 小程序分类
	miniprogram_version varchar(50) NULL, -- 小程序版本
	follow_status int2 DEFAULT 1 NULL, -- 关注状态：1-已关注 2-未关注 3-已取消关注
	follow_at timestamp NULL, -- 关注时间
	unfollow_at timestamp NULL, -- 取消关注时间
	last_use_at timestamp NULL, -- 最后使用时间
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	deleted_at timestamp NULL, -- 删除时间
	CONSTRAINT miniprogram_accounts_pkey PRIMARY KEY (account_id),
	CONSTRAINT uk_miniprogram_wxid UNIQUE (wechat_account_id, miniprogram_wxid),
	CONSTRAINT fk_miniprogram_account FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id)
);
CREATE INDEX idx_miniprogram_accounts_wechat ON public.miniprogram_accounts USING btree (wechat_account_id);
CREATE INDEX idx_miniprogram_accounts_appid ON public.miniprogram_accounts USING btree (miniprogram_appid);
CREATE INDEX idx_miniprogram_accounts_follow ON public.miniprogram_accounts USING btree (follow_status, is_deleted);
CREATE INDEX idx_miniprogram_accounts_time ON public.miniprogram_accounts USING btree (follow_at, unfollow_at);
COMMENT ON TABLE public.miniprogram_accounts IS '小程序账号表';

-- Column comments

COMMENT ON COLUMN public.miniprogram_accounts.wechat_account_id IS '关联的微信账号ID';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_name IS '小程序名称';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_wxid IS '小程序微信ID';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_appid IS '小程序AppID';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_avatar IS '小程序头像';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_description IS '小程序描述';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_category IS '小程序分类';
COMMENT ON COLUMN public.miniprogram_accounts.miniprogram_version IS '小程序版本';
COMMENT ON COLUMN public.miniprogram_accounts.follow_status IS '关注状态：1-已关注 2-未关注 3-已取消关注';
COMMENT ON COLUMN public.miniprogram_accounts.follow_at IS '关注时间';
COMMENT ON COLUMN public.miniprogram_accounts.unfollow_at IS '取消关注时间';
COMMENT ON COLUMN public.miniprogram_accounts.last_use_at IS '最后使用时间';
COMMENT ON COLUMN public.miniprogram_accounts.is_deleted IS '是否删除';
COMMENT ON COLUMN public.miniprogram_accounts.created_at IS '创建时间';
COMMENT ON COLUMN public.miniprogram_accounts.updated_at IS '更新时间';
COMMENT ON COLUMN public.miniprogram_accounts.deleted_at IS '删除时间';


-- public.official_account_search_logs definition

-- DROP TABLE official_account_search_logs;

CREATE TABLE official_account_search_logs (
	log_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	search_keyword varchar(255) NOT NULL, -- 搜索关键词
	search_type int2 DEFAULT 1 NULL, -- 搜索类型：1-公众号 2-小程序 3-混合搜索
	search_results int4 DEFAULT 0 NULL, -- 搜索结果数
	selected_official_id int8 NULL, -- 选择的公众号ID
	searched_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 搜索时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT official_account_search_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_search_log_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_search_log_official FOREIGN KEY (selected_official_id) REFERENCES official_accounts(account_id)
);
CREATE INDEX idx_search_logs_wechat ON public.official_account_search_logs USING btree (wechat_account_id);
CREATE INDEX idx_search_logs_keyword ON public.official_account_search_logs USING btree (search_keyword);
CREATE INDEX idx_search_logs_type ON public.official_account_search_logs USING btree (search_type);
CREATE INDEX idx_search_logs_time ON public.official_account_search_logs USING btree (searched_at);
COMMENT ON TABLE public.official_account_search_logs IS '公众号搜索记录表';

-- Column comments

COMMENT ON COLUMN public.official_account_search_logs.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.official_account_search_logs.search_keyword IS '搜索关键词';
COMMENT ON COLUMN public.official_account_search_logs.search_type IS '搜索类型：1-公众号 2-小程序 3-混合搜索';
COMMENT ON COLUMN public.official_account_search_logs.search_results IS '搜索结果数';
COMMENT ON COLUMN public.official_account_search_logs.selected_official_id IS '选择的公众号ID';
COMMENT ON COLUMN public.official_account_search_logs.searched_at IS '搜索时间';
COMMENT ON COLUMN public.official_account_search_logs.created_at IS '创建时间';


-- public.miniprogram_search_logs definition

-- DROP TABLE miniprogram_search_logs;

CREATE TABLE miniprogram_search_logs (
	log_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	search_keyword varchar(255) NOT NULL, -- 搜索关键词
	search_results int4 DEFAULT 0 NULL, -- 搜索结果数
	selected_miniprogram_id int8 NULL, -- 选择的小程序ID
	searched_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 搜索时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT miniprogram_search_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_miniprogram_search_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_miniprogram_search_mini FOREIGN KEY (selected_miniprogram_id) REFERENCES miniprogram_accounts(account_id)
);
CREATE INDEX idx_miniprogram_search_wechat ON public.miniprogram_search_logs USING btree (wechat_account_id);
CREATE INDEX idx_miniprogram_search_keyword ON public.miniprogram_search_logs USING btree (search_keyword);
CREATE INDEX idx_miniprogram_search_time ON public.miniprogram_search_logs USING btree (searched_at);
COMMENT ON TABLE public.miniprogram_search_logs IS '小程序搜索记录表';

-- Column comments

COMMENT ON COLUMN public.miniprogram_search_logs.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.miniprogram_search_logs.search_keyword IS '搜索关键词';
COMMENT ON COLUMN public.miniprogram_search_logs.search_results IS '搜索结果数';
COMMENT ON COLUMN public.miniprogram_search_logs.selected_miniprogram_id IS '选择的小程序ID';
COMMENT ON COLUMN public.miniprogram_search_logs.searched_at IS '搜索时间';
COMMENT ON COLUMN public.miniprogram_search_logs.created_at IS '创建时间';


-- public.official_account_messages definition

-- DROP TABLE official_account_messages;

CREATE TABLE official_account_messages (
	message_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	official_account_id int8 NOT NULL, -- 公众号ID
	message_type int2 NOT NULL, -- 消息类型：1-文本 2-图片 3-语音 4-视频 5-链接 6-文章 7-推送通知
	message_title varchar(500) NULL, -- 消息标题
	message_content text NULL, -- 消息内容
	message_url varchar(500) NULL, -- 消息链接
	message_thumbnail varchar(500) NULL, -- 消息缩略图
	direction int2 DEFAULT 1 NULL, -- 消息方向：1-接收 2-发送
	read_status int2 DEFAULT 0 NULL, -- 阅读状态：0-未读 1-已读
	read_at timestamp NULL, -- 阅读时间
	message_time timestamp NULL, -- 消息时间
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT official_account_messages_pkey PRIMARY KEY (message_id),
	CONSTRAINT fk_official_msg_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_official_msg_account FOREIGN KEY (official_account_id) REFERENCES official_accounts(account_id)
);
CREATE INDEX idx_official_msg_wechat ON public.official_account_messages USING btree (wechat_account_id);
CREATE INDEX idx_official_msg_account ON public.official_account_messages USING btree (official_account_id);
CREATE INDEX idx_official_msg_type ON public.official_account_messages USING btree (message_type);
CREATE INDEX idx_official_msg_read ON public.official_account_messages USING btree (read_status);
CREATE INDEX idx_official_msg_time ON public.official_account_messages USING btree (message_time);
CREATE INDEX idx_official_msg_direction ON public.official_account_messages USING btree (direction, is_deleted);
COMMENT ON TABLE public.official_account_messages IS '公众号消息记录表';

-- Column comments

COMMENT ON COLUMN public.official_account_messages.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.official_account_messages.official_account_id IS '公众号ID';
COMMENT ON COLUMN public.official_account_messages.message_type IS '消息类型：1-文本 2-图片 3-语音 4-视频 5-链接 6-文章 7-推送通知';
COMMENT ON COLUMN public.official_account_messages.message_title IS '消息标题';
COMMENT ON COLUMN public.official_account_messages.message_content IS '消息内容';
COMMENT ON COLUMN public.official_account_messages.message_url IS '消息链接';
COMMENT ON COLUMN public.official_account_messages.message_thumbnail IS '消息缩略图';
COMMENT ON COLUMN public.official_account_messages.direction IS '消息方向：1-接收 2-发送';
COMMENT ON COLUMN public.official_account_messages.read_status IS '阅读状态：0-未读 1-已读';
COMMENT ON COLUMN public.official_account_messages.read_at IS '阅读时间';
COMMENT ON COLUMN public.official_account_messages.message_time IS '消息时间';
COMMENT ON COLUMN public.official_account_messages.is_deleted IS '是否删除';
COMMENT ON COLUMN public.official_account_messages.created_at IS '创建时间';
COMMENT ON COLUMN public.official_account_messages.updated_at IS '更新时间';


-- public.miniprogram_messages definition

-- DROP TABLE miniprogram_messages;

CREATE TABLE miniprogram_messages (
	message_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	miniprogram_id int8 NOT NULL, -- 小程序ID
	message_type int2 NOT NULL, -- 消息类型：1-文本 2-图片 3-语音 4-视频 5-链接 6-通知
	message_title varchar(500) NULL, -- 消息标题
	message_content text NULL, -- 消息内容
	message_url varchar(500) NULL, -- 消息链接
	message_thumbnail varchar(500) NULL, -- 消息缩略图
	direction int2 DEFAULT 1 NULL, -- 消息方向：1-接收 2-发送
	read_status int2 DEFAULT 0 NULL, -- 阅读状态：0-未读 1-已读
	read_at timestamp NULL, -- 阅读时间
	message_time timestamp NULL, -- 消息时间
	is_deleted bool DEFAULT false NULL, -- 是否删除
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT miniprogram_messages_pkey PRIMARY KEY (message_id),
	CONSTRAINT fk_mini_msg_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_mini_msg_account FOREIGN KEY (miniprogram_id) REFERENCES miniprogram_accounts(account_id)
);
CREATE INDEX idx_mini_msg_wechat ON public.miniprogram_messages USING btree (wechat_account_id);
CREATE INDEX idx_mini_msg_account ON public.miniprogram_messages USING btree (miniprogram_id);
CREATE INDEX idx_mini_msg_type ON public.miniprogram_messages USING btree (message_type);
CREATE INDEX idx_mini_msg_read ON public.miniprogram_messages USING btree (read_status);
CREATE INDEX idx_mini_msg_time ON public.miniprogram_messages USING btree (message_time);
CREATE INDEX idx_mini_msg_direction ON public.miniprogram_messages USING btree (direction, is_deleted);
COMMENT ON TABLE public.miniprogram_messages IS '小程序消息记录表';

-- Column comments

COMMENT ON COLUMN public.miniprogram_messages.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.miniprogram_messages.miniprogram_id IS '小程序ID';
COMMENT ON COLUMN public.miniprogram_messages.message_type IS '消息类型：1-文本 2-图片 3-语音 4-视频 5-链接 6-通知';
COMMENT ON COLUMN public.miniprogram_messages.message_title IS '消息标题';
COMMENT ON COLUMN public.miniprogram_messages.message_content IS '消息内容';
COMMENT ON COLUMN public.miniprogram_messages.message_url IS '消息链接';
COMMENT ON COLUMN public.miniprogram_messages.message_thumbnail IS '消息缩略图';
COMMENT ON COLUMN public.miniprogram_messages.direction IS '消息方向：1-接收 2-发送';
COMMENT ON COLUMN public.miniprogram_messages.read_status IS '阅读状态：0-未读 1-已读';
COMMENT ON COLUMN public.miniprogram_messages.read_at IS '阅读时间';
COMMENT ON COLUMN public.miniprogram_messages.message_time IS '消息时间';
COMMENT ON COLUMN public.miniprogram_messages.is_deleted IS '是否删除';
COMMENT ON COLUMN public.miniprogram_messages.created_at IS '创建时间';
COMMENT ON COLUMN public.miniprogram_messages.updated_at IS '更新时间';


-- public.official_account_subscriptions definition

-- DROP TABLE official_account_subscriptions;

CREATE TABLE official_account_subscriptions (
	subscription_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	official_account_id int8 NOT NULL, -- 公众号ID
	subscription_type int2 DEFAULT 1 NULL, -- 订阅类型：1-自动订阅 2-手动订阅 3-被推送订阅
	notification_enabled bool DEFAULT true NULL, -- 是否启用通知
	subscription_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 订阅时间
	last_notification_at timestamp NULL, -- 最后通知时间
	notification_count int4 DEFAULT 0 NULL, -- 通知数量
	is_active bool DEFAULT true NULL, -- 是否有效
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 更新时间
	CONSTRAINT official_account_subscriptions_pkey PRIMARY KEY (subscription_id),
	CONSTRAINT uk_official_sub UNIQUE (wechat_account_id, official_account_id),
	CONSTRAINT fk_official_sub_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_official_sub_account FOREIGN KEY (official_account_id) REFERENCES official_accounts(account_id)
);
CREATE INDEX idx_official_sub_wechat ON public.official_account_subscriptions USING btree (wechat_account_id);
CREATE INDEX idx_official_sub_account ON public.official_account_subscriptions USING btree (official_account_id);
CREATE INDEX idx_official_sub_active ON public.official_account_subscriptions USING btree (is_active);
CREATE INDEX idx_official_sub_notification ON public.official_account_subscriptions USING btree (notification_enabled);
COMMENT ON TABLE public.official_account_subscriptions IS '公众号订阅记录表';

-- Column comments

COMMENT ON COLUMN public.official_account_subscriptions.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.official_account_subscriptions.official_account_id IS '公众号ID';
COMMENT ON COLUMN public.official_account_subscriptions.subscription_type IS '订阅类型：1-自动订阅 2-手动订阅 3-被推送订阅';
COMMENT ON COLUMN public.official_account_subscriptions.notification_enabled IS '是否启用通知';
COMMENT ON COLUMN public.official_account_subscriptions.subscription_at IS '订阅时间';
COMMENT ON COLUMN public.official_account_subscriptions.last_notification_at IS '最后通知时间';
COMMENT ON COLUMN public.official_account_subscriptions.notification_count IS '通知数量';
COMMENT ON COLUMN public.official_account_subscriptions.is_active IS '是否有效';
COMMENT ON COLUMN public.official_account_subscriptions.created_at IS '创建时间';
COMMENT ON COLUMN public.official_account_subscriptions.updated_at IS '更新时间';


-- public.miniprogram_access_logs definition

-- DROP TABLE miniprogram_access_logs;

CREATE TABLE miniprogram_access_logs (
	log_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	miniprogram_id int8 NOT NULL, -- 小程序ID
	access_type int2 DEFAULT 1 NULL, -- 访问类型：1-打开 2-分享 3-搜索 4-推荐 5-从消息打开
	access_path varchar(500) NULL, -- 访问路径/页面
	access_parameter text NULL, -- 访问参数
	duration int4 NULL, -- 访问时长（秒）
	device_id int8 NULL, -- 设备ID
	accessed_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 访问时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT miniprogram_access_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_mini_access_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_mini_access_mini FOREIGN KEY (miniprogram_id) REFERENCES miniprogram_accounts(account_id),
	CONSTRAINT fk_mini_access_device FOREIGN KEY (device_id) REFERENCES devices(device_id)
);
CREATE INDEX idx_mini_access_wechat ON public.miniprogram_access_logs USING btree (wechat_account_id);
CREATE INDEX idx_mini_access_mini ON public.miniprogram_access_logs USING btree (miniprogram_id);
CREATE INDEX idx_mini_access_type ON public.miniprogram_access_logs USING btree (access_type);
CREATE INDEX idx_mini_access_time ON public.miniprogram_access_logs USING btree (accessed_at);
CREATE INDEX idx_mini_access_device ON public.miniprogram_access_logs USING btree (device_id);
COMMENT ON TABLE public.miniprogram_access_logs IS '小程序访问记录表';

-- Column comments

COMMENT ON COLUMN public.miniprogram_access_logs.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.miniprogram_access_logs.miniprogram_id IS '小程序ID';
COMMENT ON COLUMN public.miniprogram_access_logs.access_type IS '访问类型：1-打开 2-分享 3-搜索 4-推荐 5-从消息打开';
COMMENT ON COLUMN public.miniprogram_access_logs.access_path IS '访问路径/页面';
COMMENT ON COLUMN public.miniprogram_access_logs.access_parameter IS '访问参数';
COMMENT ON COLUMN public.miniprogram_access_logs.duration IS '访问时长（秒）';
COMMENT ON COLUMN public.miniprogram_access_logs.device_id IS '设备ID';
COMMENT ON COLUMN public.miniprogram_access_logs.accessed_at IS '访问时间';
COMMENT ON COLUMN public.miniprogram_access_logs.created_at IS '创建时间';


-- public.official_account_follow_logs definition

-- DROP TABLE official_account_follow_logs;

CREATE TABLE official_account_follow_logs (
	log_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	official_account_id int8 NOT NULL, -- 公众号ID
	event_type int2 NOT NULL, -- 事件类型：1-关注 2-取消关注
	event_reason varchar(200) NULL, -- 事件原因
	event_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 事件时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT official_account_follow_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_official_follow_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_official_follow_account FOREIGN KEY (official_account_id) REFERENCES official_accounts(account_id)
);
CREATE INDEX idx_official_follow_wechat ON public.official_account_follow_logs USING btree (wechat_account_id);
CREATE INDEX idx_official_follow_account ON public.official_account_follow_logs USING btree (official_account_id);
CREATE INDEX idx_official_follow_event ON public.official_account_follow_logs USING btree (event_type);
CREATE INDEX idx_official_follow_time ON public.official_account_follow_logs USING btree (event_at);
COMMENT ON TABLE public.official_account_follow_logs IS '公众号关注事件日志表';

-- Column comments

COMMENT ON COLUMN public.official_account_follow_logs.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.official_account_follow_logs.official_account_id IS '公众号ID';
COMMENT ON COLUMN public.official_account_follow_logs.event_type IS '事件类型：1-关注 2-取消关注';
COMMENT ON COLUMN public.official_account_follow_logs.event_reason IS '事件原因';
COMMENT ON COLUMN public.official_account_follow_logs.event_at IS '事件时间';
COMMENT ON COLUMN public.official_account_follow_logs.created_at IS '创建时间';


-- public.miniprogram_follow_logs definition

-- DROP TABLE miniprogram_follow_logs;

CREATE TABLE miniprogram_follow_logs (
	log_id bigserial NOT NULL,
	wechat_account_id int8 NOT NULL, -- 所属微信账号ID
	miniprogram_id int8 NOT NULL, -- 小程序ID
	event_type int2 NOT NULL, -- 事件类型：1-关注/添加 2-取消关注/删除
	event_reason varchar(200) NULL, -- 事件原因
	event_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 事件时间
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- 创建时间
	CONSTRAINT miniprogram_follow_logs_pkey PRIMARY KEY (log_id),
	CONSTRAINT fk_miniprogram_follow_wechat FOREIGN KEY (wechat_account_id) REFERENCES wechat_accounts(account_id),
	CONSTRAINT fk_miniprogram_follow_mini FOREIGN KEY (miniprogram_id) REFERENCES miniprogram_accounts(account_id)
);
CREATE INDEX idx_miniprogram_follow_wechat ON public.miniprogram_follow_logs USING btree (wechat_account_id);
CREATE INDEX idx_miniprogram_follow_mini ON public.miniprogram_follow_logs USING btree (miniprogram_id);
CREATE INDEX idx_miniprogram_follow_event ON public.miniprogram_follow_logs USING btree (event_type);
CREATE INDEX idx_miniprogram_follow_time ON public.miniprogram_follow_logs USING btree (event_at);
COMMENT ON TABLE public.miniprogram_follow_logs IS '小程序关注事件日志表';

-- Column comments

COMMENT ON COLUMN public.miniprogram_follow_logs.wechat_account_id IS '所属微信账号ID';
COMMENT ON COLUMN public.miniprogram_follow_logs.miniprogram_id IS '小程序ID';
COMMENT ON COLUMN public.miniprogram_follow_logs.event_type IS '事件类型：1-关注/添加 2-取消关注/删除';
COMMENT ON COLUMN public.miniprogram_follow_logs.event_reason IS '事件原因';
COMMENT ON COLUMN public.miniprogram_follow_logs.event_at IS '事件时间';
COMMENT ON COLUMN public.miniprogram_follow_logs.created_at IS '创建时间';


-- DROP FUNCTION public.update_updated_at_column();

CREATE OR REPLACE FUNCTION public.update_updated_at_column()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$function$
;
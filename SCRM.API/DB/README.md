# 数据库设计文档

## 文件命名规范

所有SQL文件按照以下规范命名：
```
{序号}-{模块名}-{子模块名}.sql
```

例如：`01-device-account-management.sql`

---

## 已完成的模块

### 01-device-account-management.sql - 设备与账号管理

#### 表结构设计

##### 1. devices（设备表）
- **用途**：存储手机客户端、客服客户端等设备的基本信息
- **主要字段**：
  - device_id：设备主键ID
  - device_uuid：设备唯一标识
  - device_type：设备类型（手机/客服端）
  - sdk_version：当前SDK版本
  - os_type：操作系统类型

##### 2. wechat_accounts（微信账号表）
- **用途**：存储微信账号的基本信息
- **主要字段**：
  - account_id：账号主键ID
  - wxid：微信WXID（唯一）
  - wechat_number：微信号
  - account_status：账号状态

##### 3. device_authorizations（设备授权表）
- **用途**：管理设备与微信账号的授权关系
- **主要字段**：
  - auth_id：授权主键ID
  - device_id：关联设备ID
  - account_id：关联微信账号ID
  - access_token：访问令牌
  - auth_status：授权状态

##### 4. account_status_logs（账号状态日志表）
- **用途**：记录微信账号的状态变化历史
- **主要字段**：
  - log_id：日志主键ID
  - account_id：关联账号ID
  - status_type：状态类型（上线/下线/登出等）
  - occurred_at：发生时间

##### 5. device_version_logs（设备版本记录表）
- **用途**：记录设备SDK版本的上报历史
- **主要字段**：
  - log_id：日志主键ID
  - device_id：关联设备ID
  - sdk_version：SDK版本号
  - reported_at：上报时间

##### 6. server_redirects（服务器重定向记录表）
- **用途**：记录设备连接服务器的重定向历史
- **主要字段**：
  - redirect_id：重定向主键ID
  - device_id：关联设备ID
  - old_server_ip/port：原服务器地址
  - new_server_ip/port：新服务器地址

##### 7. device_heartbeats（设备心跳记录表）
- **用途**：记录设备心跳包，监控在线状态
- **主要字段**：
  - heartbeat_id：心跳主键ID
  - device_id：关联设备ID
  - heartbeat_at：心跳时间

---

### 02-friend-management.sql - 好友管理

#### 表结构设计

##### 1. contacts（联系人表/好友关系表）
- **用途**：存储微信账号的好友关系及好友详细信息
- **主要字段**：
  - contact_id：联系人主键ID
  - account_id：所属微信账号ID
  - friend_account_id：好友的微信账号ID
  - friend_wxid：好友微信WXID
  - remark_name：备注名
  - source_type：来源类型（搜索/群内/名片等）
  - moments_permission：朋友圈权限
  - is_star：是否星标好友
  - is_blocked：是否拉黑
  - is_deleted：是否已删除好友

##### 2. contact_tags（联系人标签表）
- **用途**：定义联系人标签
- **主要字段**：
  - tag_id：标签主键ID
  - account_id：所属微信账号ID
  - tag_name：标签名称
  - tag_color：标签颜色
  - is_system：是否系统标签

##### 3. contact_tag_relations（联系人标签关系表）
- **用途**：管理联系人与标签的多对多关系
- **主要字段**：
  - relation_id：关系主键ID
  - contact_id：联系人ID
  - tag_id：标签ID
  - tagged_at：打标签时间

##### 4. friend_requests（加好友请求表）
- **用途**：记录加好友的申请和处理过程
- **主要字段**：
  - request_id：请求主键ID
  - account_id：发起请求的微信账号ID
  - target_wxid：目标微信WXID
  - request_type：请求类型（主动/被动/群内等）
  - verify_message：验证消息
  - request_status：请求状态（待处理/已同意/已拒绝）

##### 5. contact_change_logs（联系人变更日志表）
- **用途**：记录联系人信息的变更历史
- **主要字段**：
  - log_id：日志主键ID
  - contact_id：联系人ID
  - change_type：变更类型（添加/删除/修改备注等）
  - old_value：旧值
  - new_value：新值

##### 6. contact_groups（联系人分组表）
- **用途**：管理联系人的分组（如家人、同事、客户等）
- **主要字段**：
  - group_id：分组主键ID
  - account_id：所属微信账号ID
  - group_name：分组名称
  - parent_group_id：父分组ID（支持层级）

##### 7. contact_group_relations（联系人分组关系表）
- **用途**：管理联系人与分组的多对多关系
- **主要字段**：
  - relation_id：关系主键ID
  - contact_id：联系人ID
  - group_id：分组ID

##### 8. friend_detection_logs（好友检测记录表）
- **用途**：记录僵尸粉检测任务及结果
- **主要字段**：
  - log_id：日志主键ID
  - account_id：所属微信账号ID
  - detection_type：检测类型
  - detection_result：检测结果（正常/已删除/拉黑）

#### 表关系图

```
wechat_accounts (微信账号表)
    ├── contacts (联系人表) [1:N]
    │   ├── contact_tag_relations (标签关系表) [1:N]
    │   ├── contact_group_relations (分组关系表) [1:N]
    │   └── contact_change_logs (变更日志表) [1:N]
    ├── contact_tags (标签定义表) [1:N]
    │   └── contact_tag_relations (标签关系表) [1:N]
    ├── contact_groups (分组表) [1:N]
    │   ├── contact_groups (子分组) [1:N]
    │   └── contact_group_relations (分组关系表) [1:N]
    ├── friend_requests (加好友请求表) [1:N]
    └── friend_detection_logs (检测日志表) [1:N]
```

---

### 03-message-communication.sql - 消息通信

#### 表结构设计

##### 1. messages（聊天消息表）
- **用途**：存储所有聊天消息的基本信息（核心表）
- **主要字段**：
  - message_id：消息主键ID
  - account_id：所属微信账号ID
  - msg_svr_id：微信服务器消息ID
  - conversation_id：会话ID
  - sender_wxid：发送者WXID
  - receiver_wxid：接收者WXID
  - chat_type：聊天类型（单聊/群聊/公众号）
  - message_type：消息类型（文本/图片/语音/视频等）
  - direction：消息方向（发出/接收）
  - send_status：发送状态
  - read_status：阅读状态
  - is_revoked：是否已撤回

##### 2. message_media（消息媒体文件表）
- **用途**：存储消息中的图片、视频、语音、文件等媒体资源
- **主要字段**：
  - media_id：媒体主键ID
  - message_id：关联消息ID
  - media_type：媒体类型（图片/语音/视频/文件）
  - file_path：文件存储路径
  - file_url：文件URL
  - file_md5：文件MD5值
  - duration：时长（语音/视频）
  - width/height：尺寸（图片/视频）
  - download_status：下载状态

##### 3. message_extensions（消息扩展信息表）
- **用途**：存储特殊类型消息的扩展信息
- **主要字段**：
  - extension_id：扩展主键ID
  - message_id：关联消息ID
  - extension_type：扩展类型（名片/位置/链接/小程序/红包/转账等）
  - card_wxid：名片微信WXID
  - latitude/longitude：位置坐标
  - appid：小程序AppID
  - money_amount：红包/转账金额
  - extra_data：额外数据（JSONB格式）

##### 4. message_revocations（消息撤回记录表）
- **用途**：记录消息撤回的历史信息
- **主要字段**：
  - revocation_id：撤回主键ID
  - message_id：被撤回的消息ID
  - revoke_type：撤回类型（主动/被动）
  - original_content：原始消息内容
  - revoked_at：撤回时间

##### 5. message_forwards（消息转发记录表）
- **用途**：记录消息转发的历史
- **主要字段**：
  - forward_id：转发主键ID
  - source_message_id：源消息ID
  - target_message_id：转发后的新消息ID
  - forward_type：转发类型（单条/合并/逐条）
  - target_wxid：转发目标WXID
  - forward_status：转发状态

##### 6. message_forward_details（消息转发详情表）
- **用途**：记录合并转发时的详细消息列表
- **主要字段**：
  - detail_id：详情主键ID
  - forward_id：转发记录ID
  - source_message_id：源消息ID
  - sort_order：排序序号

##### 7. voice_to_text_logs（语音转文字记录表）
- **用途**：记录语音消息转文字的结果
- **主要字段**：
  - log_id：日志主键ID
  - message_id：语音消息ID
  - text_content：转换后的文字内容
  - confidence：识别置信度
  - convert_status：转换状态

##### 8. mass_messages（群发消息记录表）
- **用途**：记录群发消息任务及执行情况
- **主要字段**：
  - mass_id：群发主键ID
  - account_id：发送账号ID
  - target_count：目标数量
  - success_count：成功数量
  - fail_count：失败数量
  - mass_status：群发状态

##### 9. mass_message_details（群发消息详情表）
- **用途**：记录群发消息的每个目标的发送详情
- **主要字段**：
  - detail_id：详情主键ID
  - mass_id：群发任务ID
  - target_wxid：目标WXID
  - message_id：实际发送的消息ID
  - send_status：发送状态

##### 10. message_sync_logs（消息同步日志表）
- **用途**：记录消息同步任务（按时间段拉取历史消息）
- **主要字段**：
  - log_id：日志主键ID
  - account_id：账号ID
  - sync_type：同步类型（全量/增量/指定时间段）
  - start_time/end_time：时间范围
  - total_count：总消息数
  - sync_status：同步状态

#### 表关系图

```
wechat_accounts (微信账号表)
    ├── messages (聊天消息表) [1:N]
    │   ├── message_media (媒体文件表) [1:N]
    │   ├── message_extensions (扩展信息表) [1:N]
    │   ├── message_revocations (撤回记录表) [1:N]
    │   ├── message_forwards (转发记录表 - source) [1:N]
    │   ├── message_forwards (转发记录表 - target) [1:N]
    │   │   └── message_forward_details (转发详情表) [1:N]
    │   └── voice_to_text_logs (语音转文字表) [1:N]
    ├── mass_messages (群发任务表) [1:N]
    │   └── mass_message_details (群发详情表) [1:N]
    │       └── messages (实际发送消息) [1:1]
    └── message_sync_logs (同步日志表) [1:N]
```

#### 设计特点

1. **消息类型支持**：支持15种以上消息类型，扩展性强
2. **媒体文件管理**：独立的媒体表，支持文件下载/上传状态跟踪
3. **扩展信息设计**：使用JSONB存储灵活的扩展数据
4. **撤回记录保留**：软删除消息，保留撤回历史
5. **转发链路追踪**：完整记录消息转发关系
6. **群发任务管理**：支持批量发送和状态跟踪
7. **语音识别集成**：记录语音转文字结果和置信度
8. **消息同步支持**：支持历史消息拉取和同步

---

## 设计原则

### 1. 数据库范式
- **第三范式（3NF）**：消除传递依赖，每个非主属性只依赖于主键
- 合理使用冗余字段提升查询性能（如contacts表中的friend_wxid）

### 2. 命名规范
- 表名使用复数形式（devices, contacts）
- 字段名使用下划线分隔（device_id, created_at）
- 外键命名：fk_{当前表}_{关联表}
- 索引命名：idx_{表名}_{字段名}

### 3. 时间戳管理
- 所有表都包含 created_at（创建时间）
- 核心表包含 updated_at（更新时间），使用触发器自动更新
- 需要追溯的操作包含业务时间字段（如added_at、deleted_at）

### 4. 软删除
- 核心表使用 is_deleted 字段进行软删除
- 包含 deleted_at 记录删除时间
- 保留历史数据用于审计和恢复

### 5. 索引优化
- 为外键字段创建索引
- 为常用查询条件创建索引
- 为时间范围查询创建索引
- 考虑复合索引提升查询效率

### 6. 数据类型选择
- 主键使用 BIGSERIAL（支持大数据量）
- 状态/类型使用 SMALLINT（节省空间）
- 布尔值使用 BOOLEAN
- 时间使用 TIMESTAMP
- 变长字符串使用 VARCHAR，长文本使用 TEXT

---

### 04-chatroom-management.sql - 群聊管理

#### 表结构设计

##### 1. chatrooms（群聊表）
- **用途**：存储群聊的基本信息
- **主要字段**：
  - chatroom_id：群聊主键ID
  - account_id：所属微信账号ID
  - chatroom_wxid：群聊WXID
  - chatroom_name：群名称
  - owner_wxid：群主WXID
  - member_count：群成员数量
  - announcement：群公告
  - chatroom_type：群类型（普通/工作/客服）
  - chatroom_status：群状态（正常/已解散/异常）
  - is_in_chatroom：是否在群聊中

##### 2. chatroom_members（群成员表）
- **用途**：存储群聊中的成员及其角色和权限
- **主要字段**：
  - member_id：成员主键ID
  - chatroom_id：群聊ID
  - member_wxid：成员WXID
  - role_type：角色（普通/管理员/群主）
  - join_time：加入时间
  - member_status：状态（在群/已退出/被移除/拉黑）
  - is_muted：是否被禁言

##### 3. chatroom_announcements（群聊公告表）
- **用途**：记录群聊的公告内容及变更历史
- **主要字段**：
  - announcement_id：公告主键ID
  - chatroom_id：群聊ID
  - title：公告标题
  - content：公告内容
  - announcement_type：公告类型（文本/图片/链接）
  - is_active：是否生效

##### 4. chatroom_invitations（群成员邀请表）
- **用途**：记录加入群聊的邀请和处理过程
- **主要字段**：
  - invitation_id：邀请主键ID
  - chatroom_id：群聊ID
  - invitee_wxid：被邀请人WXID
  - inviter_wxid：邀请人WXID
  - invitation_type：邀请类型（直接/分享/扫码/名片）
  - invitation_status：邀请状态（待确认/已接受/已拒绝）

##### 5. chatroom_settings（群聊权限配置表）
- **用途**：记录群聊的各类权限设置
- **主要字段**：
  - setting_id：配置主键ID
  - chatroom_id：群聊ID
  - allow_member_invite：是否允许成员邀请
  - allow_member_modify_name：是否允许修改群名
  - allow_join_via_qrcode：是否允许扫码加入
  - require_approval：是否需要审批
  - block_word_list：禁用词列表

##### 6. chatroom_change_logs（群信息变更日志表）
- **用途**：记录群聊信息的变更历史
- **主要字段**：
  - log_id：日志主键ID
  - chatroom_id：群聊ID
  - change_type：变更类型（群名/公告/头像/描述/状态）
  - old_value/new_value：新旧值对比

##### 7. chatroom_member_logs（群成员变更日志表）
- **用途**：记录群成员的加入、退出、移除等变更历史
- **主要字段**：
  - log_id：日志主键ID
  - chatroom_id：群聊ID
  - member_wxid：成员WXID
  - operation_type：操作类型（加入/邀请/退出/移除/拉黑）
  - role_before/role_after：角色变化

##### 8. chatroom_qrcodes（群聊二维码表）
- **用途**：管理群聊的二维码及其使用情况
- **主要字段**：
  - qrcode_id：二维码主键ID
  - chatroom_id：群聊ID
  - qrcode_url：二维码URL
  - qrcode_type：二维码类型（永久/邀请/活码）
  - scan_count：扫描次数
  - join_count：加入人数

##### 9. chatroom_mutes（群聊禁言表）
- **用途**：管理群聊中的禁言黑名单
- **主要字段**：
  - mute_id：禁言主键ID
  - chatroom_id：群聊ID
  - member_wxid：被禁言成员WXID
  - mute_type：禁言类型（临时/永久）
  - muted_until：禁言截止时间

##### 10. chatroom_blacklists（群聊黑名单表）
- **用途**：管理群聊的黑名单，防止某些用户加入
- **主要字段**：
  - blacklist_id：黑名单主键ID
  - chatroom_id：群聊ID
  - target_wxid：黑名单成员WXID
  - block_reason：拉黑原因

##### 11. chatroom_conversations（群聊会话表）
- **用途**：管理群聊的会话信息（最后消息、未读数等）
- **主要字段**：
  - conversation_id：会话主键ID
  - account_id：账号ID
  - chatroom_id：群聊ID
  - last_message_id：最后消息ID
  - unread_count：未读消息数
  - is_pinned：是否置顶

#### 表关系图

```
wechat_accounts (微信账号表)
    └── chatrooms (群聊表) [1:N]
        ├── chatroom_members (群成员表) [1:N]
        │   └── chatroom_member_logs (成员日志表) [1:N]
        ├── chatroom_announcements (公告表) [1:N]
        ├── chatroom_invitations (邀请表) [1:N]
        ├── chatroom_settings (权限配置表) [1:1]
        ├── chatroom_change_logs (变更日志表) [1:N]
        ├── chatroom_qrcodes (二维码表) [1:N]
        ├── chatroom_mutes (禁言表) [1:N]
        ├── chatroom_blacklists (黑名单表) [1:N]
        └── chatroom_conversations (会话表) [1:N]
```

#### 设计特点

1. **完整的群管理**：支持群信息、成员、公告、权限等全面管理
2. **成员角色管理**：区分群主、管理员、普通成员三个角色
3. **邀请流程完善**：支持多种邀请方式和状态跟踪
4. **历史追踪完整**：变更日志和成员日志记录所有操作
5. **二维码管理**：支持二维码的创建、统计和生命周期管理
6. **权限灵活配置**：支持多种权限组合配置
7. **禁言黑名单**：支持临时/永久禁言和黑名单管理
8. **会话管理**：集成未读计数、置顶等常用功能

---

### 05-moments.sql - 朋友圈

#### 表结构设计

##### 1. moments（朋友圈帖子表）
- **用途**：存储朋友圈的帖子信息
- **主要字段**：
  - moment_id：帖子主键ID
  - account_id：发布者微信账号ID
  - publisher_wxid：发布者WXID
  - content：文字内容
  - moment_type：内容类型（纯文字/文字+图片/视频等）
  - visibility：可见范围（所有人/仅好友/指定可见等）
  - like_count/comment_count/share_count：互动计数
  - location：发布地点
  - emotion：心情标签
  - is_allowed_comment/is_allowed_share：权限设置

##### 2. moment_media（朋友圈媒体表）
- **用途**：存储朋友圈中的图片、视频等媒体资源
- **主要字段**：
  - media_id：媒体主键ID
  - moment_id：朋友圈ID
  - media_type：媒体类型（图片/视频/缩略图）
  - file_path/file_url/cdn_url：文件地址
  - file_md5：文件MD5值
  - width/height：尺寸信息
  - sort_order：排序序号

##### 3. moment_links（朋友圈链接表）
- **用途**：存储朋友圈中的链接信息
- **主要字段**：
  - link_id：链接主键ID
  - moment_id：朋友圈ID
  - link_url：链接URL
  - link_title：链接标题
  - link_image：缩略图
  - domain_name：域名

##### 4. moment_likes（朋友圈点赞表）
- **用途**：记录朋友圈的点赞信息
- **主要字段**：
  - like_id：点赞主键ID
  - moment_id：朋友圈ID
  - liker_wxid：点赞者WXID
  - like_type：点赞类型（普通/红心等）
  - liked_at：点赞时间
  - unliked_at：取消点赞时间

##### 5. moment_comments（朋友圈评论表）
- **用途**：记录朋友圈的评论信息
- **主要字段**：
  - comment_id：评论主键ID
  - moment_id：朋友圈ID
  - commenter_wxid：评论者WXID
  - comment_content：评论内容
  - mentions：提及的人员（JSON）
  - like_count/reply_count：评论点赞数和回复数
  - is_pinned：是否置顶
  - commented_at：评论时间

##### 6. moment_comment_replies（朋友圈评论回复表）
- **用途**：记录朋友圈评论的回复信息
- **主要字段**：
  - reply_id：回复主键ID
  - comment_id：评论ID
  - replier_wxid：回复者WXID
  - target_wxid：被回复人WXID
  - reply_content：回复内容
  - replied_at：回复时间

##### 7. moment_visibility_configs（朋友圈可见范围配置表）
- **用途**：记录朋友圈的可见范围设置
- **主要字段**：
  - config_id：配置主键ID
  - moment_id：朋友圈ID
  - visibility_type：配置类型（允许/不允许）
  - target_wxid：目标WXID

##### 8. moment_like_tasks（朋友圈点赞任务表）
- **用途**：记录自动点赞朋友圈的任务
- **主要字段**：
  - task_id：任务主键ID
  - account_id：执行账号ID
  - target_wxid：目标好友WXID
  - task_type：任务类型（单次/定时/持续）
  - like_count：已点赞数
  - is_running：是否运行中

##### 9. moment_interaction_notifications（朋友圈互动通知表）
- **用途**：记录朋友圈的点赞、评论等互动通知
- **主要字段**：
  - notification_id：通知主键ID
  - moment_id：朋友圈ID
  - receiver_wxid：接收者WXID
  - notification_type：通知类型（点赞/评论/回复）
  - actor_wxid：操作者WXID
  - is_read：是否已读

##### 10. moment_operation_logs（朋友圈操作日志表）
- **用途**：记录朋友圈的各类操作历史
- **主要字段**：
  - log_id：日志主键ID
  - moment_id：朋友圈ID
  - operator_wxid：操作者WXID
  - operation_type：操作类型（发布/删除/点赞/评论等）
  - operated_at：操作时间

##### 11. moment_shares（朋友圈分享记录表）
- **用途**：记录朋友圈被分享的信息
- **主要字段**：
  - share_id：分享主键ID
  - moment_id：朋友圈ID
  - sharer_wxid：分享者WXID
  - share_target_wxid：分享目标WXID
  - share_target_type：分享目标类型（好友/群聊）
  - share_message：分享附言

#### 表关系图

```
wechat_accounts (微信账号表)
    └── moments (朋友圈帖子表) [1:N]
        ├── moment_media (媒体表) [1:N]
        ├── moment_links (链接表) [1:N]
        ├── moment_likes (点赞表) [1:N]
        ├── moment_comments (评论表) [1:N]
        │   └── moment_comment_replies (回复表) [1:N]
        ├── moment_visibility_configs (可见范围配置表) [1:N]
        ├── moment_interaction_notifications (互动通知表) [1:N]
        ├── moment_operation_logs (操作日志表) [1:N]
        └── moment_shares (分享记录表) [1:N]

moment_like_tasks (点赞任务表)
    └── account_id (关联账号) [1:N]
```

#### 设计特点

1. **丰富的内容类型**：支持文字、图片、视频、链接等多种内容
2. **灵活的可见范围**：支持全公开、仅好友、指定可见等多种模式
3. **完整的互动功能**：点赞、评论、回复、分享等全面覆盖
4. **表情和心情标签**：支持多种点赞表情和心情标签
5. **权限控制完善**：支持禁止评论、禁止分享等权限设置
6. **历史记录完整**：操作日志、互动通知全面追踪
7. **自动化任务**：支持自动点赞朋友圈的任务管理
8. **互动统计**：实时统计点赞、评论、分享数量

---

### 06-wallet-redpacket.sql - 钱包与红包

#### 表结构设计

##### 1. wallets（钱包表）
- **用途**：存储微信账号的钱包信息
- **主要字段**：
  - wallet_id：钱包主键ID
  - account_id：微信账号ID
  - balance：零钱余额
  - balance_in_bank：零钱通余额
  - frozen_balance：冻结余额
  - total_in_amount：累计转入金额
  - total_out_amount：累计转出金额
  - total_received_redpacket：累计领取红包金额
  - total_sent_redpacket：累计发送红包金额

##### 2. wallet_balance_histories（钱包余额历史表）
- **用途**：记录钱包余额的变化历史
- **主要字段**：
  - history_id：历史记录主键ID
  - wallet_id：钱包ID
  - balance_before/balance_after：变化前后余额
  - change_type：变化类型（转入/转出/领红包等）
  - related_id：关联ID（红包/转账ID）
  - changed_at：变化时间

##### 3. redpackets（红包表）
- **用途**：存储红包的基本信息
- **主要字段**：
  - redpacket_id：红包主键ID
  - account_id：发送者账号ID
  - sender_wxid：发送者WXID
  - total_amount：红包总金额
  - count：红包个数
  - received_count：已领取个数
  - redpacket_type：红包类型（普通/拼手气/定额/群红包）
  - redpacket_status：红包状态（有效/已过期/已领完）
  - conversation_type：接收方类型（好友/群聊）
  - message：祝福语

##### 4. redpacket_receives（红包领取表）
- **用途**：记录红包的领取情况
- **主要字段**：
  - receive_id：领取记录主键ID
  - redpacket_id：红包ID
  - receiver_wxid：领取者WXID
  - receive_amount：领取金额
  - receive_status：领取状态（成功/失败/已退款）
  - received_at：领取时间

##### 5. redpacket_allocations（红包分配表）
- **用途**：记录群红包中每个领取者获得的金额
- **主要字段**：
  - allocation_id：分配主键ID
  - redpacket_id：红包ID
  - receive_id：领取记录ID
  - allocated_amount：分配金额
  - luck_level：运气等级（普通/不错/手气最佳）
  - allocation_order：领取顺序

##### 6. transfers（转账表）
- **用途**：记录微信转账信息
- **主要字段**：
  - transfer_id：转账主键ID
  - account_id：发起者账号ID
  - sender_wxid：发送者WXID
  - receiver_wxid：接收者WXID
  - amount：转账金额
  - transfer_type：转账类型（主动/红包/群发）
  - transfer_status：转账状态（待收款/已收款/已拒绝）
  - fee_amount：手续费
  - sent_at/received_at：发送/收款时间

##### 7. transactions（交易记录表）
- **用途**：记录所有支付相关的交易
- **主要字段**：
  - transaction_id：交易主键ID
  - account_id：账号ID
  - transaction_type：交易类型（红包/转账/提现等）
  - amount：交易金额
  - fee：手续费
  - net_amount：净额
  - balance_before/balance_after：余额前后
  - transaction_status：交易状态
  - related_id：关联ID

##### 8. payment_methods（支付方式表）
- **用途**：记录账户的支付方式信息
- **主要字段**：
  - method_id：方式主键ID
  - account_id：账号ID
  - method_type：支付方式（银行卡/零钱/零钱通）
  - bank_name：银行名称
  - card_last_four：卡号后四位
  - is_default：是否默认
  - is_verified：是否已验证

##### 9. transaction_fees（交易手续费表）
- **用途**：记录各类交易的手续费规则和统计
- **主要字段**：
  - fee_id：手续费主键ID
  - transaction_id：交易ID
  - fee_type：手续费类型
  - fee_amount：手续费金额
  - fee_rate：手续费率

##### 10. balance_details（余额明细表）
- **用途**：详细记录余额变化的每一笔账
- **主要字段**：
  - detail_id：明细主键ID
  - account_id：账号ID
  - wallet_id：钱包ID
  - detail_type：明细类型（收入/支出/冻结）
  - amount：金额
  - balance_before/balance_after：余额前后
  - source_type：来源类型
  - occur_time：发生时间

##### 11. withdrawal_requests（提现请求表）
- **用途**：记录用户的提现申请
- **主要字段**：
  - request_id：申请主键ID
  - account_id：申请者账号ID
  - amount：提现金额
  - fee：手续费
  - net_amount：实际到账金额
  - payment_method_id：提现方式ID
  - request_status：申请状态（待审核/已批准/处理中/已到账）
  - reason：拒绝原因
  - requested_at/completed_at：申请/完成时间

#### 表关系图

```
wechat_accounts (微信账号表)
    ├── wallets (钱包表) [1:1]
    │   ├── wallet_balance_histories (余额历史表) [1:N]
    │   └── balance_details (余额明细表) [1:N]
    ├── redpackets (红包表 - 发送者) [1:N]
    │   ├── redpacket_receives (领取表) [1:N]
    │   │   └── redpacket_allocations (分配表) [1:N]
    │   └── wallet_balance_histories (变化历史) [1:N]
    ├── redpacket_receives (领取表 - 领取者) [1:N]
    ├── transfers (转账表 - 发起者) [1:N]
    ├── transfers (转账表 - 接收者) [1:N]
    ├── transactions (交易记录表) [1:N]
    ├── payment_methods (支付方式表) [1:N]
    └── withdrawal_requests (提现申请表) [1:N]
```

#### 设计特点

1. **完整的钱包管理**：零钱、零钱通、冻结余额全覆盖
2. **灵活的红包功能**：普通红包、拼手气、定额、群红包多种类型
3. **详细的交易追踪**：支持余额变化、交易记录、手续费统计
4. **多重转账支持**：支持主动转账、红包转账、群发转账
5. **完善的提现流程**：申请、审核、处理、到账全流程
6. **运气系统**：拼手气红包支持运气等级和领取顺序
7. **支付方式管理**：支持多种支付方式和银行卡信息
8. **冻结机制**：支持余额冻结和解冻，防止重复使用
9. **手续费管理**：灵活的手续费配置和统计
10. **历史记录完整**：每一笔账都有详细的历史记录

---

### 07-official-miniprogram.sql - 公众号与小程序

#### 表结构设计

##### 1. official_accounts（公众号表）
- **用途**：存储公众号的基本信息
- **主要字段**：
  - account_id：公众号主键ID
  - wxid：公众号WXID（唯一）
  - account_name：公众号名称
  - account_type：公众号类型（订阅号/服务号/企业号）
  - avatar_url：公众号头像
  - certification_status：认证状态（未认证/个人认证/企业认证）
  - follower_count：粉丝数
  - is_subscribed：用户是否已订阅

##### 2. user_official_subscriptions（用户订阅公众号表）
- **用途**：记录用户订阅的公众号关系
- **主要字段**：
  - subscription_id：订阅主键ID
  - account_id：微信账号ID
  - official_id：公众号ID
  - subscription_status：订阅状态（已订阅/已取消/被拉黑）
  - subscribed_at：订阅时间
  - is_muted：是否免打扰
  - is_top：是否置顶

##### 3. official_articles（公众号文章表）
- **用途**：存储公众号发布的文章
- **主要字段**：
  - article_id：文章主键ID
  - official_id：公众号ID
  - title：文章标题
  - content：文章内容
  - read_count：阅读数
  - like_count：点赞数
  - comment_count：评论数
  - published_at：发布时间

##### 4. official_messages（公众号消息表）
- **用途**：记录从公众号接收的消息
- **主要字段**：
  - message_id：消息主键ID
  - account_id：接收账号ID
  - official_id：公众号ID
  - message_type：消息类型（文本/图片/语音/视频等）
  - content：消息内容
  - is_read：是否已读

##### 5-11. 其他表
- official_operation_logs：公众号操作日志表
- mini_programs：小程序表
- user_mini_program_usages：用户使用小程序表
- mini_program_messages：小程序消息表
- mini_program_operation_logs：小程序操作日志表
- app_search_records：搜索记录表
- app_permissions：第三方应用权限表

#### 表关系图

```
wechat_accounts (微信账号表)
    ├── official_accounts (公众号表 - 发布者) [1:N]
    │   ├── official_articles (文章表) [1:N]
    │   ├── official_messages (消息表) [1:N]
    │   └── official_operation_logs (操作日志表) [1:N]
    ├── user_official_subscriptions (订阅关系表) [1:N]
    │   └── official_accounts (公众号) [1:N]
    ├── mini_programs (小程序表) [1:N]
    │   ├── user_mini_program_usages (使用记录表) [1:N]
    │   ├── mini_program_messages (消息表) [1:N]
    │   └── mini_program_operation_logs (操作日志表) [1:N]
    ├── app_search_records (搜索记录表) [1:N]
    └── app_permissions (权限表) [1:N]
```

#### 设计特点

1. **完整的公众号管理**：账号、文章、消息、订阅全覆盖
2. **小程序追踪**：支持小程序使用统计和消息推送
3. **订阅生命周期**：记录订阅、取消、拉黑等完整流程
4. **搜索记录**：追踪用户搜索公众号和小程序的历史
5. **权限管理**：支持第三方应用权限的授权和撤销
6. **互动统计**：文章阅读、点赞、评论等数据统计
7. **通知管理**：支持免打扰和置顶功能
8. **操作日志**：完整的用户操作审计

---

### 08-conversation-management.sql - 会话管理

#### 表结构设计

##### 1. conversations（会话表）
- **用途**：存储会话的基本信息和状态
- **主要字段**：
  - conversation_id：会话主键ID
  - account_id：所属微信账号ID
  - conversation_wxid：会话WXID
  - conversation_type：会话类型（好友/群聊/公众号）
  - target_wxid：对方WXID
  - last_message_id：最后消息ID
  - last_message_time：最后消息时间
  - unread_count：未读消息数
  - is_pinned：是否置顶
  - is_muted：是否禁止通知
  - is_archived：是否归档

##### 2. conversation_read_logs（会话已读日志表）
- **用途**：记录会话的已读状态变更历史
- **主要字段**：
  - log_id：日志主键ID
  - conversation_id：会话ID
  - read_status：已读状态（已读/未读）
  - unread_count_before：标记前的未读数
  - unread_count_after：标记后的未读数
  - read_at：已读时间

##### 3. conversation_sync_records（会话消息同步记录表）
- **用途**：记录按时间段同步聊天消息的历史
- **主要字段**：
  - record_id：同步记录主键ID
  - conversation_id：会话ID
  - sync_type：同步类型（指定时间段/增量/全量）
  - start_time/end_time：时间范围
  - message_count：同步消息数量
  - first_msg_svr_id/last_msg_svr_id：消息MsgSvrId范围
  - sync_status：同步状态（成功/失败/进行中）

##### 4. conversation_mute_settings（会话免打扰设置表）
- **用途**：管理会话的通知设置
- **主要字段**：
  - setting_id：配置主键ID
  - conversation_id：会话ID
  - is_muted：是否禁止通知
  - mute_type：禁止类型（临时/永久）
  - muted_until：禁止截止时间

##### 5. conversation_pinned_items（会话置顶管理表）
- **用途**：管理会话的置顶状态和顺序
- **主要字段**：
  - item_id：项目主键ID
  - conversation_id：会话ID
  - item_type：项目类型（会话/消息）
  - pin_order：置顶顺序

##### 6. conversation_list_push_logs（会话列表推送日志表）
- **用途**：记录会话列表推送给客户端的历史
- **主要字段**：
  - log_id：日志主键ID
  - account_id：账号ID
  - push_type：推送类型（全量/增量/单条更新）
  - conversation_count：推送的会话数
  - push_status：推送状态（成功/失败）

##### 7. conversation_operation_logs（会话操作日志表）
- **用途**：记录会话的各类操作历史
- **主要字段**：
  - log_id：日志主键ID
  - conversation_id：会话ID
  - operation_type：操作类型（标记已读/置顶/禁止通知/归档等）
  - operated_at：操作时间

##### 8-11. 其他表
- conversation_import_tasks：会话消息导入任务表
- conversation_message_index：会话消息MsgSvrId索引表
- conversation_history：会话历史记录表
- conversation_statistics：会话统计表

#### 表关系图

```
wechat_accounts (微信账号表)
    └── conversations (会话表) [1:N]
        ├── conversation_read_logs (已读日志表) [1:N]
        ├── conversation_sync_records (同步记录表) [1:N]
        ├── conversation_mute_settings (免打扰设置表) [1:1]
        ├── conversation_pinned_items (置顶管理表) [1:N]
        ├── conversation_operation_logs (操作日志表) [1:N]
        ├── conversation_list_push_logs (推送日志表) [1:N]
        ├── conversation_import_tasks (导入任务表) [1:N]
        ├── conversation_message_index (消息索引表) [1:N]
        ├── conversation_history (历史记录表) [1:N]
        └── conversation_statistics (统计表) [1:N]
```

#### 设计特点

1. **完整的会话管理**：支持会话列表、状态、通知设置的全面管理
2. **已读状态追踪**：记录会话标记为已读/未读的完整历史
3. **时间段消息同步**：支持按时间段获取消息MsgSvrId用于历史消息拉取
4. **免打扰功能**：支持临时和永久的禁止通知设置
5. **会话置顶**：支持多个会话置顶和顺序管理
6. **推送管理**：记录会话列表推送给客户端的历史
7. **消息同步追踪**：完整记录消息导入和同步任务的执行情况
8. **MsgSvrId索引**：快速查询指定时间段内的消息ID
9. **会话统计**：汇总会话数量、未读数等统计数据
10. **操作审计**：完整的会话操作日志追踪

---

### 09-device-phone-operation.sql - 设备与手机操作

#### 表结构设计

##### 1. device_commands（设备命令表）
- **用途**：存储下达给设备的控制指令
- **主要字段**：
  - command_id：命令主键ID
  - device_id：设备ID
  - command_type：指令类型（重启/清缓存/查询电量/查询存储/查询位置等）
  - command_name：指令名称
  - command_params：指令参数（JSON格式）
  - priority：优先级（低/中/高/紧急）
  - command_status：指令状态（待执行/执行中/成功/失败）
  - result_data：执行结果
  - timeout_seconds：超时秒数
  - issued_at：下达时间
  - executed_at：执行时间

##### 2. device_command_logs（设备命令执行日志表）
- **用途**：记录设备命令的执行历史
- **主要字段**：
  - log_id：日志主键ID
  - command_id：命令ID
  - execution_status：执行状态（发送中/已送达/执行中/成功/失败）
  - error_code：错误代码
  - error_detail：错误详情
  - execution_duration：执行耗时（毫秒）
  - retry_attempt：重试次数

##### 3. device_status（设备状态表）
- **用途**：存储设备的当前状态信息
- **主要字段**：
  - status_id：状态主键ID
  - device_id：设备ID
  - battery_level：电池电量（百分比）
  - storage_total/used/available：存储空间信息
  - ram_total/used/available：内存信息
  - cpu_usage：CPU使用率
  - screen_on：屏幕是否亮起
  - network_type：网络类型（WiFi/4G/5G）
  - temperature：设备温度

##### 4. device_status_history（设备状态历史表）
- **用途**：记录设备状态的变化历史
- **主要字段**：
  - history_id：历史记录主键ID
  - device_id：设备ID
  - battery_level：电池电量
  - storage_available：可用存储空间
  - temperature：设备温度
  - is_available：设备是否可用

##### 5. device_location（设备位置表）
- **用途**：存储设备位置信息
- **主要字段**：
  - location_id：位置主键ID
  - device_id：设备ID
  - latitude/longitude：坐标
  - accuracy：定位精度（米）
  - altitude：海拔
  - address：详细地址
  - location_source：位置来源（GPS/基站/WiFi）
  - located_at：定位时间

##### 6. location_sync_logs（位置同步日志表）
- **用途**：记录设备位置同步的历史
- **主要字段**：
  - log_id：日志主键ID
  - device_id：设备ID
  - location_id：位置记录ID
  - sync_type：同步类型（定时上报/按需查询/主动上报）
  - sync_status：同步状态（成功/失败）

##### 7. wechat_settings（微信设置表）
- **用途**：存储微信账号的设置信息
- **主要字段**：
  - setting_id：设置主键ID
  - account_id：账号ID
  - nickname：微信昵称
  - avatar_url：头像URL
  - signature：个人签名
  - phone_number：手机号
  - real_name_auth：是否实名认证
  - payment_status：支付状态

##### 8. wechat_settings_history（微信设置历史表）
- **用途**：记录微信设置的变更历史
- **主要字段**：
  - history_id：历史主键ID
  - account_id：账号ID
  - field_name：字段名
  - old_value/new_value：变更前后的值
  - change_type：变更类型（昵称/头像/签名）

##### 9. wechat_qrcode（微信二维码表）
- **用途**：存储微信账号的二维码
- **主要字段**：
  - qrcode_id：二维码主键ID
  - account_id：账号ID
  - qrcode_data：二维码数据
  - qrcode_image_url：二维码图片URL
  - qr_base64：Base64编码
  - qrcode_type：二维码类型（个人/分享）
  - is_current：是否为当前有效二维码
  - generated_at：生成时间

##### 10. qrcode_request_logs（二维码请求日志表）
- **用途**：记录二维码请求的历史
- **主要字段**：
  - log_id：日志主键ID
  - account_id：账号ID
  - qrcode_id：二维码ID
  - request_type：请求类型（生成/刷新/下载）
  - request_status：请求状态（成功/失败）

##### 11. device_command_statistics（设备命令统计表）
- **用途**：统计设备命令的执行情况
- **主要字段**：
  - stat_id：统计主键ID
  - device_id：设备ID
  - total_commands：总命令数
  - success_count/fail_count/pending_count：各状态计数
  - success_rate：成功率（百分比）
  - avg_execution_time：平均执行时间

#### 表关系图

```
devices (设备表)
    ├── device_commands (设备命令表) [1:N]
    │   └── device_command_logs (命令执行日志表) [1:N]
    ├── device_status (设备状态表) [1:1]
    │   └── device_status_history (设备状态历史表) [1:N]
    ├── device_location (设备位置表) [1:N]
    │   └── location_sync_logs (位置同步日志表) [1:N]
    ├── wechat_qrcode (微信二维码表) [1:N]
    │   └── qrcode_request_logs (二维码请求日志表) [1:N]
    └── device_command_statistics (命令统计表) [1:N]

wechat_accounts (微信账号表)
    ├── wechat_settings (微信设置表) [1:1]
    │   └── wechat_settings_history (设置历史表) [1:N]
    ├── device_commands (命令表) [1:N]
    ├── device_status (状态表) [1:N]
    ├── device_location (位置表) [1:N]
    ├── wechat_qrcode (二维码表) [1:N]
    └── device_command_statistics (统计表) [1:N]
```

#### 设计特点

1. **完整的手机控制**：支持重启、清缓存、查询状态等多种控制指令
2. **实时状态监控**：电量、存储、内存、CPU、温度、网络等全方位监控
3. **位置查询**：支持GPS、基站、WiFi多种定位方式，记录精度和时间
4. **命令队列管理**：支持优先级、重试、超时配置，完整的执行日志
5. **命令执行追踪**：每个命令都有详细的执行日志和状态变更记录
6. **设备状态历史**：记录设备状态变化，便于趋势分析
7. **微信设置管理**：支持昵称、头像、签名等基本信息的修改和变更追踪
8. **二维码管理**：支持生成、刷新、下载二维码，完整的请求历史
9. **位置同步**：支持定时上报、按需查询、主动上报三种模式
10. **命令统计**：实时统计命令执行成功率和性能指标

---

### 10-other-functions.sql - 其他功能

#### 表结构设计

##### 1. friend_detection_tasks（好友检测任务表）
- **用途**：记录好友检测（僵尸粉检测）任务
- **主要字段**：
  - task_id：任务主键ID
  - account_id：账号ID
  - detection_type：检测类型（僵尸粉/删除好友/拉黑列表）
  - target_count：目标检测好友数
  - detected_count：已检测好友数
  - zombie_count/deleted_count/blocked_count：各类检测结果计数
  - task_status：任务状态（待执行/执行中/已完成/已暂停）
  - progress_percentage：任务进度百分比

##### 2. friend_detection_results（好友检测结果表）
- **用途**：记录好友检测的结果详情
- **主要字段**：
  - result_id：结果主键ID
  - task_id：检测任务ID
  - friend_wxid：好友WXID
  - detection_result：检测结果（正常/僵尸粉/已删除/拉黑）
  - last_interaction：最后互动时间
  - mutual_friends_count：共同好友数
  - send_message_count/receive_message_count：消息计数
  - moments_viewed：是否查看过朋友圈
  - red_packet_sent/received：红包互动标志
  - detection_confidence：检测置信度

##### 3. friend_detection_statistics（检测统计表）
- **用途**：统计好友检测的各类数据
- **主要字段**：
  - stat_id：统计主键ID
  - account_id：账号ID
  - total_friends：总好友数
  - total_zombie/deleted/blocked/inactive：各类好友计数
  - this_month_zombie：本月新增僵尸粉

##### 4. system_notifications（系统通知表）
- **用途**：存储系统发送给用户的通知
- **主要字段**：
  - notification_id：通知主键ID
  - notification_type：通知类型（版本更新/系统通知/维护公告）
  - title/content：通知标题和内容
  - priority：优先级（低/中/高/紧急）
  - target_type：目标类型（所有用户/指定用户/指定角色）
  - notification_status：通知状态（草稿/待发送/已发送）
  - scheduled_time：计划发送时间
  - view_count/click_count：统计数据

##### 5. user_notification_reads（用户通知阅读记录表）
- **用途**：记录用户对系统通知的阅读状态
- **主要字段**：
  - read_id：记录主键ID
  - notification_id：通知ID
  - account_id：账号ID
  - is_read：是否已读
  - is_clicked：是否已点击
  - read_at：阅读时间

##### 6. version_updates（版本更新表）
- **用途**：管理应用版本更新信息
- **主要字段**：
  - update_id：更新主键ID
  - version_number：版本号
  - app_type：应用类型（手机客户端/客服工作台）
  - platform：平台（Android/iOS/Web）
  - release_notes：发布说明
  - force_update：是否强制更新
  - download_url：下载URL
  - total_downloads：总下载数

##### 7. version_download_logs（版本更新下载日志表）
- **用途**：记录用户的版本更新下载历史
- **主要字段**：
  - log_id：日志主键ID
  - update_id：更新ID
  - account_id：账号ID
  - download_status：下载状态（开始/进行中/完成/失败）
  - download_progress：下载进度百分比
  - install_status：安装状态

##### 8. version_ratings（版本评价表）
- **用途**：用户对版本的评价和反馈
- **主要字段**：
  - rating_id：评价主键ID
  - update_id：更新ID
  - rating_score：评分（1-5）
  - comment_text：评论内容
  - helpful_count：有帮助数

##### 9. feature_switches（功能开关表）
- **用途**：管理系统功能的开关状态
- **主要字段**：
  - switch_id：开关主键ID
  - feature_code：功能代码
  - is_enabled：是否启用
  - enable_percentage：启用百分比（用于灰度发布）
  - min_version/max_version：版本范围限制

##### 10. feature_switch_logs（功能开关变更日志表）
- **用途**：记录功能开关的变更历史
- **主要字段**：
  - log_id：日志主键ID
  - switch_id：开关ID
  - old_value/new_value：变更前后值
  - changed_by_user_id：变更者

##### 11. system_logs（系统日志表）
- **用途**：记录系统的各类操作和事件
- **主要字段**：
  - log_id：日志主键ID
  - log_type：日志类型（用户操作/系统事件/错误异常/安全告警）
  - log_level：日志级别（INFO/WARN/ERROR/CRITICAL）
  - operator_user_id：操作者用户ID
  - operation_description：操作描述
  - error_code/error_message：错误信息
  - execution_time：执行耗时

#### 表关系图

```
wechat_accounts (微信账号表)
    ├── friend_detection_tasks (好友检测任务表) [1:N]
    │   └── friend_detection_results (检测结果表) [1:N]
    ├── system_notifications (系统通知表) [1:N]
    │   └── user_notification_reads (阅读记录表) [1:N]
    ├── version_download_logs (版本下载日志表) [1:N]
    │   └── version_updates (版本更新表) [1:N]
    └── version_ratings (版本评价表) [1:N]

feature_switches (功能开关表)
    └── feature_switch_logs (变更日志表) [1:N]

system_logs (系统日志表) - 关联users表
```

#### 设计特点

1. **完整的好友检测**：支持僵尸粉、已删除、拉黑等多种检测类型
2. **检测结果详情**：保存每个好友的具体检测结果和互动数据
3. **检测统计分析**：汇总统计结果数据，便于趋势分析
4. **灵活的系统通知**：支持多种通知类型、优先级、目标分类
5. **通知阅读追踪**：完整的阅读和点击统计
6. **版本管理**：支持多应用、多平台的版本更新管理
7. **灰度发布**：通过百分比控制实现灰度发布
8. **用户反馈**：用户评价和反馈统计
9. **功能开关**：灵活的功能启用/禁用管理
10. **完整的审计日志**：记录所有系统操作和事件

---

### 11-permission-management.sql - 权限管理

#### 表结构设计

##### 1. users（管理员用户表）
- **用途**：存储系统管理员和后台操作用户的信息
- **主要字段**：
  - user_id：用户主键ID
  - username：用户名（唯一）
  - email/phone_number：联系方式
  - password_hash：密码哈希
  - real_name：真实姓名
  - user_status：用户状态（正常/禁用/锁定）
  - last_login_time/ip：最后登录信息

##### 2. permission_levels（权限层级表）
- **用途**：定义系统中的权限层级
- **主要字段**：
  - level_id：层级主键ID
  - level_code：层级代码
  - level_name：层级名称（平台级/BOSS级/组长级/账户级/临时级）
  - level_type：层级类型
  - level_order：层级顺序（越小权限越高）
  - parent_level_id：上级层级ID

##### 3. permission_resources（权限资源表）
- **用途**：定义系统中所有可控制的资源权限
- **主要字段**：
  - resource_id：资源主键ID
  - resource_code：资源代码（唯一）
  - resource_name：资源名称
  - resource_type：资源类型（功能/数据/敏感操作）
  - module_code：模块代码
  - requires_auth：是否需要认证

##### 4. sensitive_permissions（敏感权限表）
- **用途**：定义敏感操作权限
- **主要字段**：
  - perm_id：权限主键ID
  - permission_code：权限代码
  - permission_type：权限类型（删除聊天记录/通话录音/转账红包/微信号获取）
  - risk_level：风险等级（低/中/高/极高）
  - requires_verification：是否需要验证
  - verification_type：验证类型（密码/双因素/人工审批）
  - approval_required：是否需要审批

##### 5. roles（角色表）
- **用途**：定义系统中的各种角色
- **主要字段**：
  - role_id：角色主键ID
  - role_code：角色代码（唯一）
  - role_name：角色名称
  - permission_level_id：权限层级ID
  - is_built_in：是否内置角色

##### 6. role_permissions（角色权限关系表）
- **用途**：定义角色与权限资源的关系
- **主要字段**：
  - rel_id：关系主键ID
  - role_id：角色ID
  - resource_id：资源ID
  - is_granted：是否授权

##### 7. role_sensitive_perms（角色敏感权限关系表）
- **用途**：定义角色与敏感权限的关系
- **主要字段**：
  - rel_id：关系主键ID
  - role_id：角色ID
  - perm_id：敏感权限ID
  - is_granted：是否授权

##### 8. user_roles（用户角色关系表）
- **用途**：定义用户与角色的关系
- **主要字段**：
  - rel_id：关系主键ID
  - user_id：用户ID
  - role_id：角色ID
  - assigned_by_user_id：分配者
  - expired_at：过期时间

##### 9. temporary_permissions（临时权限表）
- **用途**：管理临时权限分配（临时金主、临时认证权限）
- **主要字段**：
  - temp_perm_id：权限主键ID
  - account_id：账号ID
  - permission_type：权限类型（临时金主/临时认证/临时操作）
  - start_time/end_time：生效和过期时间
  - usage_limit：使用次数限制
  - usage_count：已使用次数

##### 10. permission_approvals（权限审批流程表）
- **用途**：管理敏感权限的审批流程
- **主要字段**：
  - approval_id：审批主键ID
  - request_user_id：请求者
  - sensitive_perm_id：敏感权限ID
  - account_id：目标账号ID
  - approval_status：审批状态（待审批/已批准/已拒绝）
  - first_approver_id/second_approver_id：一级/二级审批人

##### 11. permission_operation_logs（权限操作日志表）
- **用途**：记录所有权限相关的操作和变更
- **主要字段**：
  - log_id：日志主键ID
  - operator_user_id：操作者
  - operation_type：操作类型（授权/撤销/修改/审批）
  - target_type：目标类型（用户/角色/账号/临时权限）
  - target_id：目标ID
  - operation_detail：操作详情（JSON）

#### 表关系图

```
users (管理员用户表)
    ├── user_roles (用户角色关系表) [1:N]
    │   └── roles (角色表) [1:N]
    │       ├── role_permissions (角色权限关系表) [1:N]
    │       │   └── permission_resources (权限资源表) [1:N]
    │       └── role_sensitive_perms (角色敏感权限关系表) [1:N]
    │           └── sensitive_permissions (敏感权限表) [1:N]
    ├── permission_approvals (权限审批表) [1:N]
    ├── permission_operation_logs (权限日志表) [1:N]
    ├── temporary_permissions (临时权限表 - 创建者) [1:N]
    └── temporary_permissions (临时权限表 - 审批者) [1:N]

permission_levels (权限层级表)
    └── roles (角色表) [1:N]

wechat_accounts (微信账号表)
    ├── temporary_permissions (临时权限表) [1:N]
    └── permission_approvals (权限审批表) [1:N]
```

#### 权限层级结构

1. **平台级权限**：服务器管理员、客服运维管理员 - 最高权限
2. **BOSS级权限**：多账户组管理 - 管理多个账户组
3. **组长级权限**：分配账户组 - 管理组内账户
4. **账户级权限**：单账户权限 - 针对具体账户
5. **临时级权限**：临时金主、临时认证权限 - 有时间限制

#### 敏感权限控制

- **删除聊天记录权限**：极高风险，需要双因素认证和人工审批
- **通话/录音权限**：高风险，需要密码验证
- **转账/红包权限**：高风险，需要人工审批
- **微信号获取展示权限**：中风险，需要密码验证

#### 设计特点

1. **多层级权限系统**：支持5层权限层级，灵活的权限控制
2. **角色基访问控制（RBAC）**：用户通过角色获得权限
3. **敏感权限保护**：风险等级分类，多级验证和审批
4. **临时权限管理**：支持有时间限制的临时权限分配
5. **权限审批流程**：支持一级和二级审批，完整的审批链路
6. **完整的操作审计**：所有权限操作都有详细的审计日志
7. **权限继承**：支持权限层级继承
8. **灵活的资源定义**：支持功能、数据、敏感操作三种资源类型
9. **实时权限检查**：支持动态权限验证和过期处理
10. **内置和自定义角色**：支持系统内置角色和自定义角色

---

## 待实现的模块

无 - 所有11个模块已全部完成 ✅

---

## 使用说明

### 执行顺序

1. 首先执行 `01-device-account-management.sql`（包含基础表和触发器函数）
2. 然后按序号执行其他模块的SQL文件
3. 确保外键依赖的表已经创建

### 注意事项

1. **外键约束**：删除数据时注意级联关系
2. **索引维护**：大量数据插入时考虑临时禁用索引
3. **触发器**：update_updated_at_column() 函数在第一个SQL文件中创建，后续文件复用
4. **权限管理**：根据实际需求配置数据库用户权限

### 性能优化建议

1. **分区表**：
   - device_heartbeats（设备心跳）：按月分区
   - messages（消息表）：按月或按季度分区
   - message_media（媒体文件）：按月分区
   - message_sync_logs（同步日志）：按月分区

2. **定期清理**：
   - 历史心跳数据：保留最近3个月，其余归档
   - 过期的好友请求：保留1年
   - 消息媒体文件：按策略定期迁移到冷存储
   - 同步日志：保留最近6个月

3. **读写分离**：高并发场景考虑主从复制

4. **缓存策略**：
   - 联系人信息使用Redis缓存
   - 最近消息列表使用Redis缓存（按会话）
   - 媒体文件URL使用CDN + Redis缓存

5. **索引优化**：
   - messages表考虑创建复合索引：(account_id, conversation_id, sent_at DESC)
   - 对于大表定期执行VACUUM和ANALYZE

6. **文件存储**：
   - 小文件（<1MB）可存储在数据库
   - 大文件存储在OSS/S3，数据库只存URL
   - 考虑使用CDN加速媒体文件访问

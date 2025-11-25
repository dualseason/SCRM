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

## 待实现的模块

- 04-chatroom-management.sql - 群聊管理
- 05-moments.sql - 朋友圈
- 06-wallet-redpacket.sql - 钱包与红包
- 07-official-miniprogram.sql - 公众号与小程序
- 08-conversation-management.sql - 会话管理
- 09-device-phone-operation.sql - 设备与手机操作
- 10-other-functions.sql - 其他功能
- 11-permission-management.sql - 权限管理

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

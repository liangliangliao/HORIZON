# Parallel Lives / Future Messages

这是 v0.4.2 的双人同起点服务：两位玩家加入同一邀请码，获得同一个种子和 12 天牌池，分别生活，再比较公开的行动与资源结果。朋友留言进入另一位玩家的延迟 TIME ECHO，不给资源加成。它不包含排行榜。

## 运行

要求 Node 20+，不需要安装第三方依赖：

```bash
cd services/parallel-lives
node --test server.test.mjs
HORIZON_SOCIAL_DATA=/absolute/private/path/parallel-lives.json node server.mjs
```

默认监听 `127.0.0.1:8788`，`HOST`、`PORT` 可配置。在自己的 HTTPS 反向代理后运行，然后在游戏的「人生模式 → 在线 Parallel Lives」填写 HTTPS 基础地址。当前仓库没有公共托管实例，因此默认地址为空；不把本地 HTTP 测试当作两台手机已联网验收。

`GET /healthz` 返回健康状态。房间保存 7 天，最多两人、每人 8 条留言；每条留言最多 200 字。每 IP 每分钟 90 次请求，单个请求体最多 32 KiB，实例默认最多 1000 个房间。

## 接口

| 操作 | 地址 | 请求 |
| --- | --- | --- |
| 建立共同起点 | `POST /v1/rooms` | `{worldSeed, name}` |
| 加入 | `POST /v1/rooms/{code}/join` | `{name}` |
| 查看 | `GET /v1/rooms/{code}` | 成员 Bearer 令牌 |
| 分享当前人生 | `POST /v1/rooms/{code}/timeline` | `{timeline}` + 成员 Bearer 令牌 |
| 留下未来消息 | `POST /v1/rooms/{code}/messages` | `{day, text}` + 成员 Bearer 令牌 |

创建和加入返回成员 ID 与随机会话令牌。服务端只保存令牌 SHA-256；本地 JSON 数据文件权限为 0600，并通过临时文件原子替换。邀请码用于加入，不能代替成员凭证读取时间线。公开时间线只包含种子、牌池版本、行动日期/名称、六项资源和完成状态；私人目标、玩家模型、现实星座与 AI 密钥不在投影中。

客户端令牌和在线练习进度单独保存在连接会话文件中，不进入人生备份。在线人生使用固定 runNumber=1、catalogVersion=10 和共同种子，保存原来的私人主线。上传会检查日期连续、资源范围、完成天数和不可回退的进度；这是叙事比较，不是防作弊竞赛。

已有 6 项真实 HTTP 服务测试覆盖两个独立客户端、成员权限、第三人拒绝、公开投影、重启恢复、种子/回退校验、过期和留言限额。公网部署、真实双设备网络和运营容量仍待验证。

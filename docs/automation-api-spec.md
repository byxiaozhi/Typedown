# Typedown 外部自动化接口：v1 协议规格

**文档状态**：v1 草案；与实施计划 v5 同步
**编写日期**：2026-09-29
**适用范围**：Windows Typedown 文档 MVP；Uno 后续复用相同契约
**规范用语**：“必须”“不得”表示互操作和可靠性要求；“建议”表示可调整的实现选择。

本文件集中定义传输、初始化、身份、并发、方法、错误码和 scope。实施顺序、测试环境与产品边界见 [automation-api-analysis-plan.md](automation-api-analysis-plan.md)，审阅处理记录见 [automation-api-review-decisions.md](automation-api-review-decisions.md)。

## 1. 传输、请求与初始化

### 1.1 请求模型

使用 JSON-RPC 2.0 作为方法、请求 ID、结果、错误和通知模型。流式传输采用类似 Language Server Protocol 的 `Content-Length` 帧：

```text
Content-Length: 73\r\n
\r\n
{"jsonrpc":"2.0","id":1,"method":"system.ping","params":{"apiVersion":1}}
```

选择长度帧而不是 NDJSON 的原因：

- JSON 字符串中的正文换行确实会转义；但合法 JSON 消息本身允许缩进和格式化换行，NDJSON 必须再增加“消息必须压成单行”的编码约束。
- 接收端可在解析 JSON 前检查精确字节长度，并对接近上限的大正文使用有界读取。
- 能明确识别半包、粘包和截断 body，不依赖逐行读取器对超长单行的实现细节。
- 与 LSP 风格工具链一致；直接调用者由参考客户端库和 CLI 隐藏 framing，脚本使用 `typedownctl --json`，无需手写 header。

因此不把“正文包含换行”本身作为长度帧的理由。若未来真实调用方证明 NDJSON 的运维收益更高，可以在 `system.initialize` 之前通过不同端点或明确的 framing 版本增加，不能在同一字节流中猜测格式。

### 1.2 版本协商

连接后的第一个请求必须是 `system.initialize`：

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "system.initialize",
  "params": {
    "apiVersion": 1,
    "client": {
      "id": "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c",
      "name": "typedownctl",
      "version": "1.0.0"
    },
    "requestedScopes": [
      "app.read",
      "document.read",
      "document.write",
      "document.save",
      "window.focus"
    ],
    "capabilities": {
      "presentationAcknowledgement": true,
      "serverRequests": false
    }
  }
}
```

响应至少包含：

- `apiVersion`
- 应用名称、版本、提交号、平台和 `buildType`（`application` 或 `automationTestHost`）；它与现有用于区分预发布版本的 `test.<run>` 标签不是同一概念
- 本次连接的 `clientSessionId`、`grantedScopes` 和带原因的 `deniedScopes`
- 支持的方法及可选能力
- `maxMessageBytes`
- 是否支持 `replaceText`、呈现近似回执、事件、偏移编辑和选区编辑
- 每个方法对应的 schema 版本或 schema 标识

未知方法和版本不兼容必须返回结构化错误，不能让服务线程退出。对象中的未知可选字段默认忽略，以便旧服务端能够接收新客户端的扩展参数；字段类型错误、未知枚举值和缺少必填字段仍返回 `invalid_params`。需要严格检查时由方法 schema 明确声明，不能由不同平台自行决定。

### 1.3 双向连接、scope 与客户端身份

JSON-RPC 连接层从第一版起必须能区分双向的请求、响应和通知，并为两个方向分别维护待完成请求表。Windows 文档 MVP 不主动向客户端发请求；未来菜单命令回调和同步钩子只有在客户端声明 `serverRequests: true`、服务端也返回相应能力后才能使用。实现不能假定“服务端永远只返回响应”，也不能在持有文档锁或 UI dispatcher 工作项时等待插件回答。

MVP 定义最小 scope 名称：`app.read`、`document.read`、`document.write`、`document.save` 和 `window.focus`。独立设置接口再增加 `settings.read`、`settings.write`。每个方法声明所需 scope；未获得时返回 `scope_required`。当前“允许本机自动化”仍是当前系统用户的总开关，尚无逐客户端审批，所以服务端会授予调用方请求且当前构建支持的 scope，并明确返回未知或关闭的 scope。这个字段为以后兼容预留，不能描述成已经有了插件沙箱。

| scope | MVP 方法或效果 |
| --- | --- |
| 无 | `system.initialize`；完成初始化后的 `system.ping` |
| `app.read` | `app.getState`、`window.list` |
| `document.read` | `document.list/get`，包括路径和未保存正文 |
| `document.write` | `document.open/create/replace/replaceText/undo/redo` |
| `document.save` | `document.save`；不能由 `document.write` 隐式获得 |
| `window.focus` | `window.focus`、`document.focus` 和任何 `reveal` 请求 |
| `settings.read` | 独立设置线的 `settings.describe/get` |
| `settings.write` | 独立设置线的 `settings.set/update` |
| `window.view` | `window.setView`：切换模式、侧栏、状态栏、专注/打字机模式，移动或调整窗口大小（v1 新增，2026-09-30） |

`client.id` 是客户端生成并持久保存的 UUID，用于连接提示、日志关联、限流和未来登记流程。它与 `client.name` 一样可以被同一用户的其他进程冒充，不能单独作为授权依据。真正的插件身份需要 Typedown 安装登记产生的 `installationId`，并由不可伪造的凭据证明；以后可以在 initialize 中增加可选 credential，而不改变现有字段。

### 1.4 机器契约原则

接口定义以 JSON Schema、标准 fixture 和行为测试为准，文档及 CLI 帮助由同一份方法描述生成或校验。v1 的 schema 是 [`automation-schema/v1.json`](automation-schema/v1.json)（每个方法的 `<method>.params` / `<method>.result`、错误对象及其 code/kind 对照），必须接受和必须拒绝的实例在 [`automation-schema/fixtures.json`](automation-schema/fixtures.json)；`Dev/Typedown.Automation.Tests` 校验 fixture、服务端实际输出，以及 schema、本节错误表与代码三者的错误码一致。每个方法必须明确：

- 参数和结果 schema、必填字段、默认值及大小限制。
- 是否只读、是否幂等、是否允许并行及是否支持取消。
- 一致性级别，以及成功返回是否代表编辑器已应用或磁盘已持久化。
- 目标对象关闭、版本冲突、超时及客户端重试时的行为。
- 是否会改变窗口焦点、活动标签、选区、撤销栈或磁盘内容。

跨语言值遵循以下规则：

- 方法名、事件名、设置 key 和枚举值使用稳定英文标识，不使用本地化文本、枚举序号或界面下标。
- 每次进程启动产生 `instanceId`；revision 是不超过 JavaScript 安全整数范围的单调 JSON number，并与 `instanceId` 一起解释。
- `contentHash` 和 `baseContentHash` 是精确正文 UTF-8 字节的 SHA-256 小写十六进制，不能复用当前仅供 UI 快速比较的短 hash。
- 时间使用带时区的 RFC 3339 字符串；持续时间和超时使用整数毫秒。
- 路径返回目标平台的规范化绝对路径，身份判断仍只依赖 `documentId`；路径大小写不能作为跨平台协议语义。
- `null`、字段缺省和空字符串分别在 schema 中定义，不由客户端猜测。
- **正文只用 `\n` 换行。** 接口读写的正文与宿主交给编辑器的文本一致：宿主载入文件时已把换行统一为 `\n`（`TextFileFormat.Normalize`），保存时再按文件原有换行写回。文件的磁盘换行作为文档元数据 `lineEnding`（`lf`、`crlf` 或 `cr`）返回。写入的正文含 `\r` 时返回 `invalid_params`，不静默转换；否则编辑器（CodeMirror）会自行改写换行并把它报告成一次编辑。`contentHash` 按 `\n` 正文计算。*（实施阶段 0a 新增：`source-stability-check.js` 发现 CodeMirror 会改写 CRLF，待确认。）*

程序接口不能依赖当前语言下的错误文案。JSON-RPC `error.code` 使用稳定整数，`error.data.kind` 使用稳定英文标识，面向人的本地化说明只放在 `error.message`。

服务端为每次已接收的写操作生成 `operationId`，用于关联宿主、WebView 回执、界面提示和诊断日志。客户端可以提供 `clientOperationId` 作为自己的关联标识；MVP 不承诺以该标识缓存和重放历史结果。JSON-RPC 请求 `id` 只关联当前连接中的响应。

正文修改依靠 `baseRevision` 和 `baseContentHash` 防止盲目重放：第一次成功后 revision 前进，同一旧请求再次到达时必须冲突。若连接在响应到达前断开，客户端先重新连接并读取目标 revision 与 hash；不能直接重试 `document.create` 等非幂等操作。需要跨连接自动重试的真实调用方出现后，再增加有界结果缓存，并单独定义缓存生命周期和进程崩溃语义。

MVP 为每个方法定义服务端超时，并在连接断开时取消尚未开始的排队操作。显式 JSON-RPC 取消通知延后到出现确实需要长时间运行的方法时再设计；任何取消都只能作用于提交前，已经改变文档或设置后不能报告成“未执行”。


## 2. 对象身份、并发与完成语义

### 2.1 标识

外部接口至少需要以下稳定标识：

| 标识 | 生命周期 | 用途 |
| --- | --- | --- |
| `instanceId` | 服务进程启动至退出 | 区分重启前后的 revision 和事件序号 |
| `clientId` | 客户端安装/配置保留期间 | initialize 中 `client.id` 的自报 UUID，只用于关联，不能证明身份 |
| `clientSessionId` | 一次连接完成初始化至断开 | 服务端生成，区分同一客户端的并发连接 |
| `windowId` | 窗口创建至关闭 | 区分多窗口 |
| `documentId` | 标签文档创建至关闭 | 区分未命名文档和同路径的会话 |
| `revision` | 文档生命周期内单调递增 | 防止旧客户端覆盖新正文 |
| `settingsRevision` | 进程生命周期内单调递增；只随外部设置（`settings.describe` 列出的那些）的改动和恢复默认前进，窗口位置、侧栏、模式、最近文件等程序自己保存的内部值不计入 | 观察和批量修改设置 |
| `operationId` | 一次已接收的内部写操作 | 由服务端生成，关联 WebView 回执、界面提示和诊断 |
| `clientOperationId` | 由调用方决定 | 可选关联标识；MVP 不以它提供跨连接结果缓存 |
| `eventSequence` | 服务进程生命周期内单调递增 | 后续事件接口用于检测丢失并建立快照水位 |

文件路径不是文档身份：未命名文档没有路径，另存为会改变路径，路径大小写和符号链接也会导致歧义。Windows 版应为 `DocumentTab` 增加稳定 `documentId`，与 Uno 已有模型对齐。

`loadId` 继续作为宿主与 WebView 之间丢弃迟到消息的内部标识，不直接作为外部 `revision`。两者语义不同：切换或重新加载会改变 `loadId`，真实正文变化才改变 `revision`。

每个写结果携带 `origin`（`user`、`automation`、`filesystem`、`restore` 或 `system`）和服务端 `operationId`。后续事件接口也携带这些字段；自动化事件可附带自报的 `clientId`、服务端 `clientSessionId` 和 `clientOperationId`，使程序识别自己的写入，避免反馈环。授权判断不能只使用自报 `clientId`。

### 2.2 写入规则

所有正文写操作必须携带调用方最后看到的 `baseRevision`：

```json
{
  "jsonrpc": "2.0",
  "id": 18,
  "method": "document.replace",
  "params": {
    "clientOperationId": "agent-step-23",
    "documentId": "a67c...",
    "baseRevision": 42,
    "text": "# 新正文\n",
    "normalizationPolicy": "requireKnownSafe",
    "save": false,
    "reveal": "document",
    "awaitPresentation": true
  }
}
```

若当前版本不是 42，返回 `revision_conflict` 及当前版本，不执行修改。客户端必须重新读取、合并后重试。

每个文档使用独立的异步修改锁。写操作在所属窗口的 UI 调度队列上执行，顺序为：

1. 确认窗口、文档和标签仍存在。
2. 若目标是当前活动文档，刷新 WebView 最新正文。
3. 再次检查 `baseRevision`。
4. 为操作分配服务端 `operationId` 和候选目标 revision；保存写入前正文、光标、滚动位置，并暂存撤销事务，但不提交 history、revision、脏状态或派生状态。
5. 活动文档通过带 `operationId`、目标 revision、内部 `loadId` 和 `baseContentHash` 的编辑命令交给 WebView；后台文档更新自己的标签快照。
6. WebView 在同一个页面事件中先强制完成自己的节流上报，再把当前实时正文 hash 与 `baseContentHash` 比较。用户若在宿主检查 revision 后又输入了内容，页面拒绝操作并返回冲突，不能应用旧基准上的编辑。
7. 校验通过后，活动 WebView 在 Muya/CodeMirror 应用候选内容，并回传它持有的精确源文本 hash、Muya 初始序列化文本 hash 和规范化分类。应用和恢复期间产生的页面变化必须携带 `operationId` 并从普通用户 `MarkdownChange` 流中隔离，不能提前推进宿主 history 或 revision。
8. 第 7 步开始后，若应用错误、结果未知的超时、规范化策略拒绝候选或 WebView 崩溃，宿主丢弃候选正文、候选 revision 和暂存的撤销事务。恢复只有一条路径：使用第 4 步保存的权威正文、光标和滚动位置，通过正常 `LoadFile` 流程和新的内部 `loadId` 重新载入；WebView 崩溃时先重建 WebView。第 7 步之前检测到的 revision/hash 冲突没有改变页面，直接返回冲突即可。恢复加载不改变外部 revision，也不重置宿主持有的 history。
9. 宿主等待恢复加载的 `FileLoaded`/刷新回执，并逐字校验 `baseContentHash`。恢复成功后返回原始操作错误；只有宿主驱动的恢复本身也失败时才返回 `editor_inconsistent`，隔离该文档并拒绝后续写入。失败操作的页面处理器不承担自行恢复责任。
10. 成功收到正确 `operationId` 和 `loadId` 的 `DocumentEditApplied` 后，宿主一次性提交正文、撤销事务、保存状态和 revision，再更新标题、大纲、字数、光标等派生状态。
11. 根据 `save` 决定是否保存；若请求 `awaitPresentation`，页面再等待两次 `requestAnimationFrame` 后返回近似呈现回执。

整个候选应用阶段不能把 Muya 的序列化文本静默当成调用方写入的正文，也不能让中间态进入自动保存或崩溃备份。只有第 10 步提交后的权威正文才对这些服务可见。

不能因为刷新超时就假定宿主快照是最新正文。写接口遇到刷新超时应返回 `editor_not_ready` 或 `content_sync_timeout`，不能继续覆盖。

修改后台文档默认不抢占用户焦点，只更新标签快照和未保存标记；人在切换到该标签时通过正常 `LoadFile` 握手看到新内容。调用方确实希望人立即看到目标文档时，显式传入 `reveal: "document"`，服务端再激活窗口、切换标签并等待呈现回执。

`reveal: "document"` 不改变页面的滚动位置：编辑器保留读者的光标和滚动位置，改在屏幕外的内容只能从标题提示得知。调用方希望人看到改了哪里时，传 `reveal: "change"`（v1 新增，2026-10-02）：先做 `"document"` 的一切，页面应用写入后再把第一个改动的顶层块滚进视野——它的开头已在窗口内，并且整块放得下或开头在窗口上半部分时不滚；否则把开头放到窗口自上而下三分之一处。纯删除滚到删除处现在所在的块（删在末尾时是最后一块）。源码模式按改动的起始字符滚动。读者的光标和选区不动，只移动画面。`"change"` 对 `document.open`、不带正文的 `document.create` 和 `document.undo/redo` 与 `"document"` 相同：打开没有改动可示；撤销和重做本来就把光标放回历史记录的位置并滚到那里。它和 `"document"` 一样需要 `window.focus`；`"document"` 在 `document.open/create` 上一直没有检查这个 scope，v1 内不能再收紧（第 6.2 节），新值在所有方法上都检查。

三种模式遵守同一正文规则：宿主的文档状态保存权威源文本；源码模式把它精确交给 CodeMirror；可视和阅读模式可建立规范化的内部模型，但在用户没有真实编辑时必须保留源文本。阅读模式允许外部写入并重新渲染。模式切换先刷新待上报输入，并与该文档的外部写操作串行；不能在一次写入尚未确认时卸载当前编辑器。

当前 `7c02d10` 通过 `importedRef` 保存 Muya 导入前的源文本：只要 Muya 当前输出仍等于初始 `normalized`，读取时就返回原始 `source`。因此“无用户编辑时逐字读回”主要验证该映射在模式切换、异步渲染和刷新之间是否稳定，不能证明人第一次可视编辑后的序列化一定保真。

每次正文写入都必须在成功结果和后续 `document.get` 元数据中公开 `pendingNormalization`：

| 值 | 判定与处理 |
| --- | --- |
| `none` | `source` 与 `normalized` 逐字相同 |
| `knownFormatting` | 差异完全属于有 fixture 证明的保守白名单转换，且受保护载荷逐字保留；允许提交 |
| `unknown` | 两者不同且尚未分类，或后台文档尚无 Muya 实例可计算 `normalized`；默认策略拒绝，调用方显式选择 `allowUnknown` 时才允许提交并持续公开风险 |
| `unsafe` | 已确认首次可视编辑会损失内容或结构；不作为活动编辑器写入的成功结果，已有文档或后台文档延迟分类后可处于此状态，可视模式显示不阻断的提示 |

已确认会丢失内容或结构的输入不产生成功结果，返回 `content_not_roundtrippable` 并走第 8～9 步的宿主恢复路径。分类器必须有版本号；初版可以把除逐字相同和少量已证明转换以外的差异都归入 `unknown`，不能为了提高 `knownFormatting` 命中率而降低证据要求。

正文写方法的 `normalizationPolicy` 默认是 `requireKnownSafe`：只提交 `none` 或 `knownFormatting`；`unknown` 返回 `normalization_unclassified`，正文和 revision 不变。确实需要后台写入或接受现有 Muya 边界的程序可以显式传 `allowUnknown`，但成功结果仍保留 `unknown`，Typedown 在第一次可视编辑前显示非阻塞提示。`unsafe` 在任何策略下都拒绝。这样分类能力不足不会被误报为内容损坏，也不会在默认路径被当成安全。

不能只用同一个 Markdown 解析器分别渲染 `source` 和 `normalized`，再比较 DOM 或纯文本，就宣称两者语义相同。同一个解析缺陷可能同时影响两侧；纯文本和渲染树还可能遗漏链接目标、图片 URL、代码围栏语言、脚注标识、任务状态、原始 HTML 属性、front matter、公式和其他不可见载荷。`knownFormatting` 必须基于明确允许的差异及独立保留检查；解析或渲染等价只能作为附加证据。

### 2.3 “调用成功”与“人已经看到”的区别

内部实现仍要区分排队、应用、呈现和持久化，避免状态混淆；MVP 的公开返回语义收敛为两项保证和一个可选观察结果：

| 公开语义 | 保证 |
| --- | --- |
| 普通写操作成功 | 权威内存状态和当前活动编辑器都已应用目标 revision；响应返回 `revision`、`contentHash` 和服务端 `operationId` |
| `save: true` 或 `document.save` 成功 | 在上一项基础上，目标 revision 已通过现有原子保存路径写入磁盘 |
| `awaitPresentation: true` | 仅在配合 `reveal: "document"` 或 `"change"` 时有效；返回 `presentation`，说明窗口/标签是否可见、页面是否经过两帧、宿主 dispatcher 是否经过一次呈现机会 |

普通成功不能只表示“消息已经发给 WebView”。后台文档没有活动编辑器可确认时，成功表示标签快照已应用；调用方若要求人在当前窗口观察，必须使用 `reveal: "document"`。

`presentation` 是观察近似值，不是事务提交级别。它不能证明窗口没有被遮挡、桌面合成器已经把像素送到显示器或人确实看到了变化。超时响应必须同时说明正文是否已经应用，调用方据此读取状态，不能盲目重放写操作。

### 2.4 Windows MVP 实现说明

- **写入前等待加载确认。** 宿主在把写入交给页面前，先等页面对当前 `loadId` 回报 `FileLoaded`；否则 `document.open` 后立即写入会与加载竞争。`consistency: "latest"` 读取同样先等加载确认再刷新。
- **写入期间读者的输入。** 页面应用候选后、宿主提交前，读者在页面里的输入由宿主暂存；提交（以及请求的保存）完成后再作为下一个 revision 应用。因此写入的 `save` 写盘的是写入的 revision，不含读者尚未保存的输入；写入失败恢复时暂存输入随被撤回的候选一起丢弃；写入期间标签已被切走时也丢弃，不会落到另一个文档。
- **后台标签。** 后台标签没有 Muya 实例，写入的 normalization 为 `unknown`（`notEvaluated`），只有 `allowUnknown` 才提交；本版 `document.save`、`document.undo/redo` 只作用于窗口当前显示的文档，后台标签返回 `editor_not_ready`（`data.reason: "notActive"`），需先 `document.focus`。
- **连接标记在窗口标题。** 状态栏可被用户关闭，标题不能：有客户端连接时标题附加“自动化已连接”，写入后约 4 秒显示“{客户端} 编辑了 {文档}”（客户端名去掉控制字符和双向控制符，最长 40 字符）。
- **页面规范化查询。** `document.get` 通过 `QueryNormalization`/`NormalizationReport` 向页面取当前的规范化判定。
- **写入与模式切换、标签切换、页面重载。** 页面在同一个事件里完成比较和应用，所以写入在页面一侧没有可被打断的中间态。页面应用之后、宿主提交之前（测试宿主屏障 `afterEditorMutationBeforeReport`）可能出现三种情况：
  - **切换模式**：可视、源码、阅读三种模式都从页面持有的正文重新挂载，那已经是候选正文，所以照常提交，三种模式都显示写入的正文（E2E W02）。
  - **WebView 重载**（页面出错、渲染进程崩溃）**或标签切走又切回**：页面从宿主重新载入的是提交前的旧正文。宿主发现页面的 `loadId` 已经变了，就不提交，返回 `editor_not_ready`（`data.reason: "editorReloaded"`），并按第 8～9 步恢复（E2E W01、W03）。
  - **标签切走、此后没有切回**：候选正文提交进该标签的快照，切回时经正常 `LoadFile` 显示（E2E W03）。
- **呈现回执。** `awaitPresentation` 只能与 `reveal: "document"` 或 `"change"` 同用，否则 `invalid_params`（`reason: "requiresReveal"`）。写入提交（及请求的保存）后，宿主最多等 3 秒：页面收到 `AwaitPresentation` 后经过两次 `requestAnimationFrame` 回 `PresentationFrames`，宿主同时等一次 `CompositionTarget.Rendering`，再读窗口可见（可见且未最小化）和标签是否仍是当前标签。四项都成立时结果带 `presentation`；否则返回 `presentation_timeout`，`data` 带 `applied: true`、已提交的 `revision`、`contentHash`、`operationId`、`saved` 和观察到的 `presentation`。
- **滚动到改动处。** `reveal: "change"` 让 `ApplyDocumentEdit` 带上 `scrollToChange: true`。页面用和高亮相同的块比较找出第一个改动的块；局部替换直接滚动，整篇重新载入（引用定义变了等情况）时把“保持原滚动位置”的逐帧保持改为保持在改动处，否则那段保持会把画面拉回原处（E2E RV01）。
- **测试宿主。** `buildType=automationTestHost` 的构建以 `--automation-test-root <dir>` 启动，数据、日志、WebView2、互斥体、交接管道和自动化端点都与日常实例分开，端点名写入 `<dir>\automation-endpoint.txt`；它另有 `test.barrier.*`、`test.editor.pageText`、`test.window.open`、`test.settings.get/set`。

## 3. 方法与设置契约

### 3.1 系统与应用

| 方法 | 类型 | 行为 |
| --- | --- | --- |
| `system.initialize` | 查询 | 协商版本和能力 |
| `system.ping` | 查询 | 检查进程及接口响应 |
| `app.getState` | 查询 | 返回应用版本、活动窗口和基本状态 |
| `window.list` | 查询 | 列出窗口、活动状态和文档数量 |
| `window.focus` | 命令 | 激活指定窗口 |
| `window.getView` | 查询 | 窗口的显示方式：模式、侧栏、状态栏、专注/打字机模式、屏幕上的外框和是否最大化（`app.read`） |
| `window.setView` | 命令 | 改变上面任意几项（`window.view`）；返回改变之后的显示方式 |

`window.setView` 的参数都可选，但至少给一项，否则 `invalid_params`（`reason: nothingToChange`）：

- `mode`：`visual`（可视）、`source`（源码）或 `reading`（阅读）。
- `sidePane: {open?, page?}`：`page` 是 `files` 或 `outline`。
- `statusBar`、`focusMode`、`typewriter`：布尔值。
- `bounds: {x?, y?, width?, height?}`：窗口外框，屏幕像素；宽至少 480、高至少 320、都不超过 16384，坐标在 ±32768 内。最大化或最小化的窗口先还原。

语义：

- 模式、侧栏、状态栏、专注和打字机就是“视图”菜单改的那几项，和人在那个窗口里点菜单一样生效、一样被记住。Windows 上模式是每个窗口自己的（B01），Uno 上是应用级的。外框只属于这个窗口。
- 改模式与这个窗口当前文档的自动化写入串行：写入进行中时，模式切换等它提交或放弃后才执行，不会插在写入中间（E2E V01）。改完模式后，等编辑器重新显示文档再返回。
- 返回时新的显示已经画出来：页面又画了两帧；Uno 的 WebView 在 X11 上是独立的原生窗口，调整布局后会晚一步移动，所以 Uno 还要等页面视口连续几次都等于布局给它的大小。之后立即截图，得到的就是新视图。
- 正文、revision 和保存状态都不变。

### 3.2 文档

| 方法 | 阶段 | 行为 |
| --- | --- | --- |
| `document.list` | MVP | 列出文档 ID、窗口、路径、标题、版本和保存状态，不返回全文 |
| `document.get` | MVP | 按显式一致性级别返回快照或刷新后的最新正文；可选返回与同一 revision 对应的标题列表 |
| `document.open` | MVP | 通过现有打开流程打开文件，返回目标窗口和文档 ID |
| `document.create` | MVP | 创建未命名文档，可带初始正文；成功后返回稳定文档 ID 和 revision |
| `document.focus` | MVP | 激活文档所在窗口和标签 |
| `document.replace` | MVP | 带 `baseRevision` 替换全文，形成一个撤销步骤 |
| `document.replaceText` | MVP | 在精确正文中计数并替换固定字符串，计数不符时不修改 |
| `document.save` | MVP | 保存现有路径；无路径时返回 `path_required`，不弹出文件选择器 |
| `document.close` | v1 新增 | 关闭一个已保存的文档；有未保存更改时返回 `unsaved_changes`，不丢弃、不弹对话框（2026-10-01） |
| `document.saveAs` | 后续 | 只接受调用方给出的明确路径，不由后台调用弹出对话框 |
| `document.undo` / `redo` | MVP | 调用文档自己的历史栈 |
| `document.applyEdits` | 后续 | 协商位置编码后按范围应用一组互不重叠编辑 |
| `selection.get` | 后续 | 获取当前选区和选中文本 |
| `selection.replace` | 后续 | 按文档版本替换当前选区 |

`document.close { documentId, baseRevision? }`（`document.write`）关闭文档所在的标签。先取编辑器的最新正文（尚未上报的输入也算），文档有未保存的更改就返回 `unsaved_changes` 和当前 revision，什么都不关：接口永远不替人丢弃内容，也不弹出保存对话框，要关先 `document.save`。给了 `baseRevision` 而文档已不在该 revision 时返回 `revision_conflict`。窗口里只剩这一个文档时，窗口留下，换成一个新的空白未命名文档：关闭文档不会关闭窗口或退出程序。成功返回 `{ documentId, windowId, closed: true }`，之后该 `documentId` 是 `document_not_found`。

`document.get` 不使用含糊的隐式一致性。调用方传入：

- `consistency: "snapshot"`：立即返回宿主快照，结果带 `isCurrent`，适合状态面板。
- `consistency: "latest"`：活动文档必须完成 WebView 刷新；失败则返回错误，适合编辑前读取。

`includeText: false` 只返回低成本元数据。调用方可用 `include: ["text", "headings"]` 请求正文和标题；每个标题至少返回稳定于该 revision 的顺序、层级、纯文本和 slug。MVP 不在标题结果中混入尚未协商单位的源码偏移。

写方法接受 `normalizationPolicy: "requireKnownSafe" | "allowUnknown"`，默认前者。成功结果包含 `operationId`、`revision`、`contentHash`、`saved` 和 `normalization`，请求了可见呈现时再包含 `presentation`。`normalization` 至少包含 `pendingNormalization`、`sourceHash`、可空的 `normalizedHash`、`reasons`（稳定英文原因数组）和 `classifierVersion`；后台标签尚未经过 Muya 时是 `unknown`、`reasons: ["notEvaluated"]` 和 `normalizedHash: null`，所以只有显式 `allowUnknown` 才能提交。`document.get` 在这个边界仍存在时返回同一组元数据。后台标签第一次可视加载必须在允许用户输入前完成分类；若结果为 `unsafe`，保留权威源文本，并在可视模式顶部显示不阻断的提示（首次可视编辑可能改写部分原始 HTML，需要保持原样请用源码模式）。*（2026-09-30 决定：不阻止进入可视模式，改为修复已知的丢失——代码块信息串与 Tab——并对剩余情况提示。）*写入后在没有用户编辑的情况下，`document.get(consistency: "latest")` 必须逐字返回调用方提交的文本，不能用规范化文本替代它。文件打开时同理：文档第一次载入（打开文件、恢复的标签第一次显示）时，编辑器交回的正文若与给它的不同，宿主不把改写结果当作文件的正文，而是用原文再载入一次；第二次仍被改写才按编辑器给出的结果为准（这时首次编辑提示照常出现）。改写的规模和种类（表格行、HTML、代码围栏，不含正文）写入日志。*（2026-10-08：一次会话恢复时整篇文档的表格被重新排版、原始 HTML 被改写，并被当成了文件正文；E2E LD01。）*

`document.replaceText` 使用固定字符串，不使用正则。`find` 不能为空；`expectedCount` 必填，服务端按 ordinal、从左到右、互不重叠地计数，数量不符时返回 `match_count_mismatch` 并保持 revision 不变，数量一致时替换全部匹配项。常见 AI 编辑因此无需计算 Unicode 偏移，同时仍由 `baseRevision` 防止在旧正文上定位。

偏移式 `document.applyEdits` 不进入 MVP。后续实现时由 `system.initialize` 协商 `positionEncoding`（至少明确 `utf-8`、`utf-16` 或 `utf-32` 中支持哪些），范围始终针对同一次 latest 读取返回的精确字符串，服务端不隐式换算 CRLF/LF。fixture 覆盖中文、emoji、代理对、组合字符和不同换行格式。

### 3.3 设置（独立交付线）

| 方法 | 行为 |
| --- | --- |
| `settings.describe` | 返回允许访问的设置名、scope、类型、默认值、枚举范围、可见效果和是否需要重启；不暴露内部处理器名称 |
| `settings.get` | 按 scope 和目标 ID 获取一个或多个白名单设置 |
| `settings.set` | 带 `baseSettingsRevision`、目标 scope 和可选 `clientOperationId` 修改一个设置 |
| `settings.update` | 后续能力；在集中设置服务中校验一批值并作为一个 revision 应用 |

设置不能通过反射自动全部公开。应建立显式描述表，至少排除：

- 密码、令牌和未来新增的秘密字段。
- 仅供迁移使用的内部标志。
- 可能破坏数据库或目录边界的内部路径。
- 没有稳定行为承诺的临时选项。

设置接口第一批只实现 `settings.set`，不作为文档 MVP 的完成条件。`settings.update` 依赖集中设置 store 和事务化通知，不能在当前每属性独立写入模型上伪装成原子操作。后续批量更新必须先校验全部值，再形成一个 `settingsRevision` 和一批变更事件；任一值无效时全部不应用。

设置线首批候选使用下表作为规范映射的起点；实现时它与 `settings.describe` 由同一份描述数据生成，不能手写两套。全部是应用级偏好，必须广播到每个窗口：

| 外部 key | 外部值 | Windows 映射 | Uno 映射 | 应用规则 |
| --- | --- | --- | --- | --- |
| `appearance.theme` | `{kind:"builtIn", id:string}`，内置 ID 为 `system`、`light`、`dark`、`black`；或 `{kind:"custom", id:string}` | 内置走 `ApplyBuiltInTheme`（`system`→`AppTheme.Default`）；自定义走 `ApplyCustomTheme`，共同维护 `AppTheme` + `CustomTheme` | 内置共同设置 `Theme` + 清空 `CustomTheme`；自定义按主题 metadata 设置 `Theme` + `CustomTheme` | 验证主题存在；刷新所有窗口、编辑器 CSS、菜单和已打开设置页 |
| `editor.fontSize` | integer，8～48 | `FontSize`（内部为 `double`） | `FontSize`（`int`） | 所有 WebView 应用；已打开设置页同步 |
| `editor.lineHeight` | number，1.0～3.0 | `LineHeight` | `LineHeight` | 统一舍入规则后广播 |
| `editor.textDirection` | `auto`、`ltr` 或 `rtl` | `TextDirection` | `TextDirection` | 所有活动和后续打开的编辑器生效 |

`SourceCode`、`ReadOnly` 更接近窗口/文档显示状态，不作为上述应用级设置直接开放；它们由 3.1 的 `window.setView` 按窗口改变（2026-09-30 实现）。

上表是首批。2026-09-30 起 `settings-map.json` 另外开放 26 个：界面语言 `ui.language`，编辑器（字体、编辑区宽度、Tab 宽度、段落标记、括号/引号/Markdown 符号自动配对），Markdown 输出（表格列对齐、列表缩进、松散列表、去掉代码块多余空行），拼写检查，标签栏、大纲自动展开、字数统计方式，图片相对路径的四个选项，以及紧凑模式、两个 Mica 效果、动画和“关闭窗口后保持运行”。都立即生效，不需要重启。“关闭窗口后保持运行”（`app.keepRunning`）的默认值在所有版本都是 `false`（2026-10-08 起；此前打包版默认 `true`）：设置的默认值属于应用行为，不在第 6 节的冻结范围内，`settings.describe` 返回当前的默认值。某个平台没有的设置不在它的 `settings.describe` 里，读写时返回 `setting_not_exposed`（`reason: notOnThisPlatform`）；平台有这个设置但不支持某个值时（例如 Windows 的列表缩进没有 `tab`，不支持 Mica 的系统打开 Mica），返回 `setting_invalid` 并说明原因。

`settings.set` 成功表示运行时模型已应用且设置快照已持久化，返回 `settingsRevision` 和服务端 `operationId`。需要重启的设置由 `settings.describe` 标出，首批默认不开放。调用方请求 `awaitPresentation` 时，才额外返回各窗口和编辑器的近似呈现状态。

### 3.4 事件

事件不进入 Windows MVP。CLI、端到端测试和第一版 MCP 用 `document.list/get` 的 revision 做有界轮询；真实调用方证明需要持续观察后，再增加以下接口。

客户端通过 `events.subscribe` 选择事件，服务端用 JSON-RPC notification 推送。订阅响应必须原子地返回当前状态快照和 `eventSequence` 水位；该连接随后只接收更大的序号，避免“先查询再订阅”之间漏掉变化。

| 事件 | 主要字段 |
| --- | --- |
| `window.opened` / `closed` / `activated` | `windowId` |
| `document.opened` / `closed` / `activated` | `windowId`, `documentId`, `path` |
| `document.changed` | `eventSequence`, `documentId`, `revision`, `saved`, `origin`, `operationId`, 可选变更范围 |
| `document.savedStateChanged` | `documentId`, `revision`, `saved` |
| `selection.changed` | `documentId`, `revision`, 选区范围，不默认携带选中文本 |
| `settings.changed` | `settingsRevision`, 设置名和脱敏后的新值 |

正文变化事件不得在每次按键时发送整篇正文。服务端按文档合并短时间内的重复事件，建议窗口为 100～250 ms；客户端需要全文时调用 `document.get`。

每个连接使用有界事件队列。慢客户端导致队列满时：

1. 优先合并相同文档的 `document.changed` 和 `selection.changed`。
2. 不丢失打开、关闭和保存状态等生命周期事件。
3. 仍无法恢复时发送带最后连续序号的 `events.resyncRequired`，清空可重建事件；客户端重新订阅并取得新的原子快照。

### 3.5 CLI 契约

`typedownctl` 是协议参考客户端，也是脚本的稳定入口。所有命令支持 `--json`，成功时 stdout 只输出一个 JSON 结果，失败时 stderr 可输出面向人的说明，stdout 仍可选择输出结构化错误。稳定退出码至少包括：

| 退出码 | 含义 |
| ---: | --- |
| 0 | 成功 |
| 2 | CLI 参数或本地输入无效 |
| 3 | Typedown 未运行、接口关闭或连接失败 |
| 4 | API 版本、能力或 scope 不兼容 |
| 5 | 目标窗口/文档不存在 |
| 6 | revision、文本匹配数或设置 revision 冲突 |
| 7 | 编辑器未就绪或同步超时 |
| 8 | 保存或设置持久化失败 |
| 9 | 其他服务端错误 |

CLI 退出码只表示错误大类；程序需要精确原因时读取 JSON-RPC `error.code` 与 `error.data.kind`。


## 4. 错误模型

JSON-RPC 的 `error.code` 必须是整数。除 `-32602` 等标准错误外，v1 在 server error 范围内定义稳定整数，并把可读标识放入 `error.data.kind`：

| `error.code` | `error.data.kind` | 含义 |
| ---: | --- | --- |
| `-32001` | `not_initialized` | 连接尚未完成版本协商 |
| `-32002` | `unsupported_version` | API 版本不兼容 |
| `-32003` | `scope_required` | 当前连接未获得该方法要求的 scope |
| `-32601` | `method_not_found` | 方法不存在；Release 对所有 `test.*` 方法必须如此返回 |
| `-32602` | `invalid_params` | 类型、范围或必填字段错误 |
| `-32010` | `window_not_found` | 窗口已关闭或 ID 无效 |
| `-32011` | `document_not_found` | 文档已关闭或 ID 无效 |
| `-32012` | `revision_conflict` | `baseRevision` 已过期 |
| `-32013` | `editor_not_ready` | WebView 尚未加载或正在恢复 |
| `-32014` | `content_sync_timeout` | 无法确认宿主持有最新正文 |
| `-32015` | `read_only` | 当前策略禁止修改 |
| `-32016` | `path_required` | 保存操作需要显式路径 |
| `-32017` | `save_failed` | 文件保存失败，正文仍保持未保存状态 |
| `-32018` | `content_not_roundtrippable` | 保守校验已确认候选序列化会损失内容或结构；宿主已恢复权威旧正文，revision 和 history 未提交 |
| `-32019` | `match_count_mismatch` | `replaceText` 的实际匹配数与 `expectedCount` 不同 |
| `-32020` | `setting_not_exposed` | 设置未列入外部白名单 |
| `-32021` | `setting_invalid` | 设置值不满足类型或范围 |
| `-32022` | `persistence_failed` | 运行时值可能已应用，但持久化失败 |
| `-32023` | `presentation_timeout` | 状态已应用，但一个或多个界面目标未及时确认呈现 |
| `-32024` | `editor_inconsistent` | 候选操作失败后，宿主使用权威旧正文执行的恢复加载也未确认；文档进入隔离状态 |
| `-32025` | `normalization_unclassified` | 默认安全策略下无法证明候选只有已知格式变化；正文和 revision 未提交，调用方可审查后显式选择 `allowUnknown` |
| `-32026` | `unsaved_changes` | `document.close` 的文档有未保存的更改；什么都没关闭，`data.revision` 是当前 revision（v1 新增，2026-10-01） |
| `-32030` | `message_too_large` | 请求超过协商上限 |
| `-32031` | `busy` | 文档操作队列已达到限制 |
| `-32032` | `request_cancelled` | 操作在提交前被取消 |

错误的 `data` 可以包含当前 `revision`、状态是否已应用、各呈现目标、实际匹配数、允许范围、规范化元数据或可重试标志，但不能包含正文、凭据或内部异常堆栈。`normalization_unclassified` 必须返回 hash、原因和分类器版本，供程序决定是否以 `allowUnknown` 重新发起一次基于当前 revision 的新操作。`presentation_timeout` 必须说明状态是否已经应用；客户端应重新读取 revision 和 hash，MVP 不提供按旧 `operationId` 查询结果的缓存接口。


## 5. 安全、隐私与资源限制

### 5.1 默认策略

- 自动化接口默认关闭，在设置页提供“允许本机自动化”开关。
- 开启后只允许当前系统用户连接。
- Windows 命名管道必须显式设置当前用户 SID 的 ACL，不能依赖默认 ACL。
- Uno socket 创建后立即设置为 `0600`，路径位于当前用户的 runtime 目录。
- v1 不开放 TCP 监听；`127.0.0.1` 也不作为默认传输，因为其他同机环境和浏览器更容易误接入。
- 客户端断开时取消该连接尚未开始的请求和全部订阅。
- 至少一个客户端连接时，所有窗口状态栏显示“自动化已连接”；接口写入时短暂显示经过长度限制和转义的客户端名称。该提示不能被客户端关闭。
- `requestedScopes` 只限制连接可调用的方法；在没有安装登记和凭据前，同一系统用户仍可自报新的 `clientId` 重新请求 scope，不能把它宣传为逐插件安全隔离。
- 服务端主动请求只发送给已声明并获授相应能力的连接；钩子超时后必须采用事先定义的安全默认行为。
- barrier 和 `test.*` 方法只存在于独立测试宿主/程序集，不能只靠运行时开关隐藏。正式应用项目和 Release 打包项目不得引用测试程序集。
- 测试宿主的窗口与版本标签必须包含醒目的 `AUTOMATION TEST HOST`，`system.initialize.buildType` 必须返回 `automationTestHost`；正式应用无论 Release、Debug 或带现有 `test.<run>` 预发布标签，都返回 `application`。

### 5.2 数据最小化

- 日志只记录方法名、请求 ID、耗时、消息字节数和错误类别。
- 不记录正文、选区文本、剪贴板内容、完整路径参数或设置秘密。
- `settings.describe/get` 从源头排除秘密字段，而不是返回后再遮盖。
- `document.list` 默认返回路径；后续若增加网络网关，需要单独增加路径脱敏策略。

### 5.3 资源限制

- 在读取正文前协商 `maxMessageBytes`，初始建议 32 MiB，并允许构建时调整。
- 限制单连接并发请求数、事件队列长度和每秒写操作数。
- 解析 JSON 前验证帧长度；格式错误或持续超限的连接直接关闭。
- 正文写入仍受现有文件保存、备份和路径校验流程保护。

## 6. 兼容政策与弃用流程（v1 冻结）

`apiVersion: 1` 自本节起冻结。冻结的范围是调用方能依赖的一切：方法名及其参数和结果（[`automation-schema/v1.json`](automation-schema/v1.json)）、错误 `code` 与 `data.kind`、scope 名称、`typedownctl` 的命令和退出码、`typedownctl mcp` 的工具名和参数、正文与 revision 的语义（第 2 节）。冻结时的 schema 另存为 [`automation-schema/v1-frozen.json`](automation-schema/v1-frozen.json)；`SchemaCompatibilityTests` 逐项比较两者，只允许下面列出的新增。

### 6.1 v1 内允许的改动

- 新增方法。它出现在 `system.initialize` 返回的方法列表里，客户端据此判断，不按应用版本号猜。
- 给已有方法新增**可选**参数；缺省时行为与之前完全相同。
- 在结果或错误 `data` 里新增字段。客户端必须忽略不认识的字段（第 1.2 节）。
- 给已有的枚举新增取值，只限结果中的枚举，并且新值只在调用方通过参数或能力要求时才出现；参数中的枚举新增取值等同于新增可选行为。
- 新增错误 `kind`，只用于新方法或新参数引出的新情况；已有情况的 `code` 和 `kind` 不变。
- 平台差异只通过 `capabilities` 和方法列表表达，不改变同一方法在不同平台上的语义（第 4 阶段的等价场景 `Tools/AutomationE2E/equivalence` 检查这一点）。

### 6.2 v1 内不允许的改动

- 删除或改名方法、参数、结果字段、枚举值、scope。
- 改变字段类型，或把可选参数改成必填，或收紧此前接受的输入。
- 改变成功所代表的保证（例如“成功表示编辑器已应用”“`save: true` 成功表示已写盘”），或改变默认策略（例如 `normalizationPolicy` 默认 `requireKnownSafe`）。
- 让已有方法需要更多 scope。
- 改变 `typedownctl` 已有命令的退出码或 `--json` 输出结构，改变 MCP 服务器（`typedownctl mcp`）已有工具的名称或参数。

这些改动只能进入 `apiVersion: 2`。

### 6.3 弃用流程

1. 在 schema 中给被替代的方法或参数加 `"deprecated": true` 和 `"x-replacedBy"`，在 `system.initialize` 的结果里用 `deprecated` 列出（方法名、替代者、最早移除的 API 版本）。被弃用的方法在整个 v1 内照常工作。
2. 发布说明写明弃用和替代方式；`typedownctl` 在调用被弃用的方法时向 stderr 打印一行提示，不改变 stdout 和退出码。
3. 移除只发生在新的 `apiVersion`。引入 v2 的版本同时继续接受 v1 的 `system.initialize`，至少保留两个次版本且不少于六个月；这段时间里，请求已不支持的版本时，`unsupported_version` 的 `data.supportedVersions` 列出仍可用的版本。
4. 冻结副本只在开始 v2 时另存为 `v2-frozen.json`；`v1-frozen.json` 不再改动。

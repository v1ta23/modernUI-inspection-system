# WinForms 项目合并与架构方案

## 1. 目标

当前有两个项目：

- `app1`：原有业务项目，包含已有功能、数据结构、业务流程
- `app2`：当前 WinForms UI demo，用来展示新的界面风格

目标不是把两个项目的代码直接拼在一起，而是以 `app2` 的界面为基础，逐步承接 `app1` 的业务能力，最终形成一个可持续扩展的新项目结构。

本方案优先解决三个问题：

1. 如何平滑合并 `app1` 和 `app2`
2. 如何在后续新增很多功能时避免窗体代码失控
3. 如何选择适合 WinForms 的开发方式

## 2. 结论

推荐采用以下原则：

- 架构采用轻量分层，风格上按 MVC 理解，但实现上不引入重型 MVC 框架
- WinForms 层只负责界面展示和事件转发
- 业务逻辑放入 `Service`
- 数据访问放入 `Repository`
- 界面流程编排放入 `Controller`
- 开发流程采用“规格驱动为主，核心逻辑测试为辅”

一句话总结：

> 用 `app2` 替换旧界面，用 `app1` 提供业务能力，中间通过分层和 Controller 解耦，而不是把逻辑继续堆进 Form。

## 3. 为什么不直接硬上重 MVC 框架

WinForms 不是 Web 框架，天然不是围绕路由、请求、控制器生命周期设计的。

如果为了“后续功能多”而强行引入复杂 MVC 框架，通常会带来这些问题：

- 项目结构看起来规范，但实际开发变慢
- 窗体事件和框架生命周期不自然
- 简单功能也要写很多胶水代码
- 新项目阶段容易过度设计

因此本项目采用“轻 MVC 分层”：

- `View`：窗体、用户控件、界面组件
- `Controller`：处理界面事件、页面加载、用户操作流程
- `Service`：封装业务规则
- `Repository`：封装数据访问
- `Model`：实体、DTO、查询条件、状态对象

这套结构足够支撑后续新增很多功能，同时不会把 WinForms 用得过重。

## 4. 推荐的最终目录结构

建议把最终解决方案整理成 3 个项目：

```text
Solution
├─ App.Core
│  ├─ Models
│  ├─ DTOs
│  ├─ Interfaces
│  ├─ Services
│  └─ Common
├─ App.Infrastructure
│  ├─ Persistence
│  ├─ Repositories
│  ├─ ExternalServices
│  └─ Config
└─ App.WinForms
   ├─ Views
   ├─ Controllers
   ├─ ViewModels
   ├─ Components
   ├─ Resources
   └─ Program.cs
```

### 4.1 各项目职责

#### `App.Core`

放稳定、可复用、与 UI 无关的内容：

- 业务实体
- 业务规则
- 服务接口
- 核心服务实现
- DTO / 参数对象 / 返回对象

这里不允许依赖 WinForms。

#### `App.Infrastructure`

放与外部资源交互的代码：

- 数据库访问
- 本地文件存储
- HTTP 调用
- 配置读写
- 第三方系统适配

这里实现 `App.Core` 中定义的接口。

#### `App.WinForms`

放所有界面层代码：

- 窗体
- 用户控件
- 主题与样式
- Controller
- 少量只服务于界面的 ViewModel

这里允许依赖 `App.Core`，也允许依赖 `App.Infrastructure` 的组装入口，但不应该直接写业务规则。

## 5. WinForms 中的轻 MVC 落地方式

### 5.1 View 层职责

View 只做这些事：

- 展示数据
- 接收用户输入
- 抛出事件
- 调用 Controller
- 根据返回结果更新界面

View 不做这些事：

- 不直接写 SQL
- 不直接操作文件
- 不写复杂业务判断
- 不在按钮点击事件里串完整业务流程

### 5.2 Controller 层职责

Controller 负责：

- 响应页面加载
- 响应按钮点击、查询、保存、删除等操作
- 调用一个或多个 Service
- 把结果整理成 View 可直接使用的数据
- 决定成功、失败、提示信息、刷新时机

### 5.3 Service 层职责

Service 负责：

- 核心业务规则
- 校验逻辑
- 状态流转
- 多个 Repository 的组合调用

### 5.4 Repository 层职责

Repository 负责：

- 数据查询
- 数据保存
- 数据删除
- 屏蔽底层数据库或存储实现

## 6. 一个典型功能应该怎么写

以“用户列表查询”为例：

1. 用户打开页面
2. `UserListForm` 调用 `UserController.Load()`
3. `UserController` 调用 `IUserService.GetList(query)`
4. `UserService` 调用 `IUserRepository`
5. Repository 返回数据
6. Service 做必要规则整理
7. Controller 转成界面可直接绑定的模型
8. View 渲染表格

按钮点击“保存”也同理：

1. View 收集输入
2. Controller 接收输入模型
3. Service 做校验和业务处理
4. Repository 持久化
5. Controller 返回结果
6. View 提示成功并刷新

## 7. app1 与 app2 的合并策略

## 7.1 原则

- 不直接把 `app1` 全量代码复制进 `app2` 的窗体
- 先提炼业务能力，再让新 UI 调用
- 每次只迁移一个功能闭环

## 7.2 推荐步骤

### 第一阶段：并存

- 保留 `app1` 和 `app2`
- 建一个新的解决方案，把两个项目都纳入
- 不急着删旧代码

目的：

- 先看清哪些逻辑可复用
- 降低一次性大迁移风险

### 第二阶段：抽核心

从 `app1` 中提炼到 `App.Core` / `App.Infrastructure`：

- 实体
- DTO
- 业务服务
- 仓储接口与实现
- 配置读取

优先抽“稳定且和 UI 无关”的部分。

### 第三阶段：接入新 UI

把 `app2` 的 demo 页面逐步改造成真实页面：

- 给每个页面补一个 Controller
- 给每个真实功能接上 Service
- 用真实数据替代 demo 假数据

### 第四阶段：替换旧界面

- 当某个模块在新 UI 中已经可用
- 再下线 `app1` 中对应的旧界面

不要先删旧界面再重写，否则回退成本太高。

### 第五阶段：收尾

- 统一入口
- 清理废弃窗体和重复逻辑
- 清理临时适配代码
- 补文档和测试

## 8. 推荐的迁移顺序

先迁移“低风险、高频、依赖少”的功能：

1. 列表页
2. 详情页
3. 新增/编辑
4. 删除/状态切换
5. 报表/统计
6. 复杂联动流程

不建议一开始就迁移：

- 权限系统
- 大量跨模块联动
- 最复杂的流程页

因为这些最容易把迁移节奏拖死。

## 9. 开发方式：规格驱动还是测试驱动

推荐：

- 页面和流程开发：规格驱动
- 核心业务：测试驱动或至少测试覆盖

## 9.1 为什么不用“全量 TDD”

WinForms 界面层天然不适合全量 TDD，原因包括：

- 控件事件较重
- 界面交互测试成本高
- 很多 UI 变化不值得为其写大量测试

如果对所有层都强推 TDD，成本会很高，速度反而下降。

## 9.2 推荐的实际做法

每开发一个功能，先写一页简化规格：

- 功能名称
- 目标用户
- 页面入口
- 输入项
- 输出项
- 主流程
- 异常流程
- 校验规则
- 成功/失败提示

然后再写代码。

对以下内容补测试：

- 金额、数量、状态计算
- 业务校验规则
- 状态流转
- 关键查询条件拼装
- Service 的主要分支

UI 层只保留必要的手工验证，不追求高测试覆盖率。

## 10. 推荐的代码规范

### 10.1 Form 代码限制

每个 Form 应遵守：

- 不直接访问数据库
- 不直接写业务规则
- 不直接 new 一堆底层依赖
- 按钮点击事件中只做转发

允许的内容：

- 读取控件值
- 调用 Controller
- 渲染结果
- 显示提示

### 10.2 Controller 规范

- 一个页面对应一个 Controller，或一个主功能对应一个 Controller
- Controller 不直接操作控件实例细节
- Controller 返回面向界面的结果对象

### 10.3 Service 规范

- Service 方法按业务动作命名
- 不要把 Service 写成“万能工具类”
- 校验和业务规则优先放在 Service

### 10.4 Repository 规范

- 一个聚合或核心实体对应一个 Repository
- Repository 不承担业务判断
- 查询条件使用明确的参数对象，不要参数乱飞

## 11. 依赖方向

必须保证依赖方向单向：

```text
WinForms -> Core -> Interfaces
WinForms -> Infrastructure(通过组装或依赖注入使用)
Infrastructure -> Core
Core -/-> WinForms
```

禁止：

- `Core` 引用 `WinForms`
- `Repository` 调用窗体
- `Form` 里直接写数据库实现细节

## 12. 依赖注入建议

项目进入分层后，建议尽早接入 .NET 内置依赖注入。

最小目标：

- 在 `Program.cs` 中统一注册
- Form、Controller、Service、Repository 用构造函数注入

这样后续新增功能时，不会到处 `new` 对象，替换实现也更容易。

## 13. 当前项目的落地建议

当前仓库还是一个单项目 demo，下一步建议按下面顺序落地：

1. 先把当前 `WinFormsApp2` 改造成 `App.WinForms`
2. 新增 `App.Core`
3. 新增 `App.Infrastructure`
4. 把 demo 中假数据替换为由 Controller 提供的数据
5. 再开始从 `app1` 抽取真实业务

如果短期内不想一次拆成 3 个项目，也可以先在当前项目内部模拟分层：

```text
WinFormsApp2
├─ Controllers
├─ Services
├─ Repositories
├─ Models
└─ Views
```

等结构稳定后，再拆分成独立项目。

这比一开始就大改解决方案更稳。

## 14. 第一阶段最小落地方案

建议先做一个最小版本，不要一次做太大：

### 14.1 第一步

在当前项目中增加目录：

- `Views`
- `Controllers`
- `Services`
- `Repositories`
- `Models`

### 14.2 第二步

把当前 `MainForm` 放入 `Views`

### 14.3 第三步

为 `MainForm` 增加一个 `MainController`

`MainController` 负责：

- 页面初始化数据
- 卡片数据加载
- 底部区域数据加载

### 14.4 第四步

把当前硬编码数据从窗体中拿出来，放到：

- `MainService`
- 或临时 `FakeDashboardRepository`

### 14.5 第五步

选 `app1` 的一个真实模块接入，完成第一个闭环

推荐第一个闭环只做：

- 查询
- 展示

不要一上来就做复杂编辑流程。

## 15. 风险与约束

### 风险 1：界面迁移过快

如果一次迁移太多模块，会导致：

- demo 页面很多
- 真实功能很少
- 到处是半成品

解决方式：

- 每次只迁移一个完整功能闭环

### 风险 2：分层名义上存在，代码实际上仍堆在 Form

解决方式：

- 在代码评审中明确禁止把业务逻辑继续写回窗体

### 风险 3：过度设计

解决方式：

- 先上轻 MVC 分层
- 先做最小依赖注入
- 先解决真实业务接入
- 不急着上复杂插件化、事件总线、领域框架

## 16. 最终建议

本项目建议采用：

- 架构：WinForms + 轻 MVC 分层
- 合并方式：先抽业务，再接新 UI，不直接拼代码
- 开发方式：规格驱动为主，核心逻辑测试覆盖
- 演进路线：先在当前项目内部分层，稳定后再拆成多个项目

这是当前阶段最稳、成本最低、又能支撑后续持续加功能的方案。

## 17. 下一步实施建议

文档确认后，建议立刻进入第一轮结构调整：

1. 在当前项目中创建 `Views / Controllers / Services / Repositories / Models`
2. 把 `MainForm` 移到 `Views`
3. 增加 `MainController`
4. 把硬编码展示数据搬出 `MainForm`
5. 预留 `IService` / `IRepository` 接口
6. 再选择 `app1` 的一个实际功能开始迁移

如果后续继续推进，第二份文档应补：

- 模块拆分清单
- 页面迁移优先级
- `app1` 现有能力梳理表
- 命名规范
- DTO / Service / Repository 示例

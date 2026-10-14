# 商店评论整理（2026-09-25 抓取）

来源：Microsoft Store 评分接口，产品 9P8TCW4H2HB4，各市场合计 209 条（2022-03 至 2026-09），评分 ★5:145 ★4:39 ★3:5 ★2:5 ★1:15。
商店上架版本仍是 1.2.18.0，2023-05 以后的评论基本都针对它。下表保留有实质内容的评论（去掉只写"好用/垃圾"的）。
原始数据：接口 `storeedgefd.dsx.mp.microsoft.com/v9.0/ratings/product/9P8TCW4H2HB4?market=CN&locale=zh-CN&deviceFamily=Windows.Desktop&pageSize=25&skipItems=N`。

## 归并后的事项（按提及次数），以及本 fork 的现状

| 事项 | 提及 | 现状（fork, 2026-09-25） |
|---|---|---|
| 主题 / 自定义 CSS / 字体 / 颜色（深色模式表格边框、浅色行内代码对比度、标题层级区分） | 13 | 已有自定义主题文件和自定义 CSS、字体设置；深浅色细节未逐条核对 |
| 崩溃 / 无响应（大文档快速输入、最小化后、移动文件、图片标题、大纲、启动） | 11 | 大文档和大纲这两类本周已处理；其余未复现 |
| 打印 / 导出 PDF（打印崩溃 5 条、PDF 图片丢失、无书签、长代码行截断、公式字体） | 11 | 未动，打印崩溃是最集中的一条 |
| 大纲（点击不跳转 5 条、不跟随、折叠、与文件树同显、默认显示） | 10 | 点击跳转、跟随、折叠本周已做；同显/右侧未做 |
| 启动慢 / 卡顿 / 内存 | 8 | 大文档卡顿已做；启动耗时未量 |
| 编辑行为（改标题级别波及相邻段、列表切换变引用、撤销闪烁、Ctrl+方向键选区、Ctrl+F 无反应、滚轮） | 8 | 未动 |
| 图片（网络图片不显示、上传配置太极客、缩放不自由、img width 无效、编辑标题崩溃） | 8 | 图床按你说的先不做；其余未动 |
| 无法启动 / 编辑区空白（缺 WebView2、ARM64、360 误报、重装后打不开） | 8 | 安装包已换 exe；WebView2 缺失提示未做 |
| 代码块（行号、写完直接保存丢失、粘贴缩进、复制丢格式、字号不作用于代码块、vue/jsx 高亮） | 8 | 未动 |
| 文件树（新建不刷新、单击不稳、显示 txt、恢复上次文件、外部修改无提示） | 6 | 大目录已做，会话恢复已有；其余未核对 |
| 页内锚点 / Ctrl+点击链接不跳转 | 5 | 未动 |
| 分栏（源码/预览）/ 只读阅读模式 | 4 | 阅读模式本周已做；分栏未做 |
| `==text==` 高亮不渲染 | 3+1 | **未支持**：Muya 的行内语法里没有这条规则 |
| 多标签页 | 3 | 已有 |
| 自动保存 / 断电、死机后文件被写空 | 3 | 自动保存已有；写空文件那条要核对保存是否原子写入 |
| 表格单元格（删空后无法输入、单元格内换行） | 2 | 未动 |
| docx 导出、mermaid、TOC、exe 安装包 | 各 2 | mermaid、TOC、exe 已有；docx 未做 |

## 评论明细

| 日期 | 评分 | 市场 | 评论 |
|---|---|---|---|
| 2026-08-25 | 5 | CN | 我说体验咋这么好，原来是wpf+winui的xaml岛+webview2，除了内存占用高点用户体验居然还可以 |
| 2026-08-07 | 5 | CN | 如果有设置选项让文件树显示所有文件，且可以编辑所有普通文本文件就更好了。 |
| 2026-07-31 | 5 | US | Thanks the developer~❤ |
| 2026-07-28 | 5 | CN | 重度使用很长时间后发现了以下问题： 1. 使用代码块写代码后不进行任何操作，直接点保存会无法保存代码块部分，必须在写完代码块后进行其他文本编辑操作 2. 插入图片编辑标题信息时可能发生崩溃 3. 希望支持HTML+CSS在编辑器内的正常渲染 4. 有些时候PageUP和PageDown不能正常滑动页面，会在某个范围来回… |
| 2026-07-20 | 5 | CN | 很好用，没有捆绑、广告等内容 |
| 2026-07-08 | 5 | CN | 其它的类似软件一堆，下了快10种，就这个好，各位不用找了 |
| 2026-07-08 | 1 | HK | 不能用垃圾软件！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！ |
| 2026-07-03 | 5 | CN | 必须感谢！但请千万别收费。 |
| 2026-06-26 | 5 | CN | 确实及其好用。。。渲染准确，编辑方便，提示清晰 |
| 2026-06-14 | 5 | CN | 第一次在Microsoft store 评价，真好用 |
| 2026-06-04 | 4 | CN | 难道是我电脑太老的原因吗？可这个应用体量不大啊，2.4Ghz的双核四线程的i5，8G的ram，英特尔500G的固态硬盘 |
| 2026-05-11 | 1 | US | Can't print out of the app. It just crashes when I click print. |
| 2026-05-09 | 5 | US | easy install, very clean minimal interface. I mostly wanted a MD reader but the editing functions seem very complete & robust |
| 2026-04-30 | 5 | CN | 东西是好用，还免费。但是ms的商店下载速度太慢了。 |
| 2026-04-18 | 3 | CN | 平时用来写一些教程，或者自己的代码库很舒服。但是用了一段时间后也发现了问题。第一，选中逻辑存在BUG，当我想要选中一段文字，改变它的格式时，比如从“段落”改为“二级标题”，往往跟这段文字紧邻的上下文的格式也会被改变，但是我并不希望他们改变，我只希望改变我选中的文字。只有当这段文字的上下文都是空格的时候，我才能选中它之后… |
| 2026-04-14 | 5 | CN | 另外，希望增加功能： 1 编辑时能自定义颜色：比如源代码模式下的标题颜色、加粗颜色、高亮颜色能在非源代码模式时也显示出来。 2 增加侧边栏显示在右边的设置开关。 |
| 2026-03-13 | 5 | CN | 非常好用，稳定可靠，还支持LaTex公式 |
| 2026-03-02 | 4 | RU | - Нельзя изменить шрифт используемый приложением - При выводе на печать выдает ошибку (использую из под учетной записи не имеющей прав администратора) |
| 2026-02-17 | 4 | CN | 默认字体颜色太深，有些刺眼，能在设置里直接选择字体颜色就好 |
| 2026-02-14 | 4 | CN | 移动文档或文件夹时，app会失去响应，只能在任务管理器中强行结束 |
| 2026-02-06 | 4 | CN | 界面简单，支持各种格式，还支持PDF导出。这些功能对我足够用了 |
| 2026-01-26 | 4 | HK | 1、基本功能完善，轻巧简约，但软件内的反馈功能失效，这里成了唯一反馈渠道； 2、左侧目录，单击文件打开文件功能不稳定，有时候可以打开，有些文件打不开，有时候又能随意都打开，建议想vs code那样，单击文件不打开，双击才会打开，打开的文件斜体标注等； 3、建议增加至少一种新的模板，现在的样式太朴素，各级标题看上去差不多… |
| 2025-12-17 | 5 | CN | 整体来说用着很舒服，不过建议修复一下代码粘贴进去不是 4Tab 的 bug |
| 2025-12-13 | 5 | CN | 建议添加能直接打卡txt文档的功能 |
| 2025-12-10 | 5 | CN | 该有的都有，感觉是很不错的markdown界notepad++。下载后的第一感受：好险，差点下单某pora了;) |
| 2025-12-02 | 4 | US | Displays images (good!), but wraps lines on single line breaks (bad). |
| 2025-11-05 | 5 | CN | 不过浅色模式下感觉内联代码块的背景和文字的背景有点区分不开，看不出来插入了行内代码 |
| 2025-11-01 | 5 | CN | 开源免费，感恩作者 感恩(´ᴗ`ʃƪ) |
| 2025-10-05 | 5 | CN | 怎么设置为默认文本编辑器？ |
| 2025-09-24 | 1 | US | It doesn't really integrate into Windows if you can't print a document. Pretty bad programming. |
| 2025-09-05 | 1 | CN | Rubbish with out Microsoft WebView2 Runtime and I can't use |
| 2025-08-07 | 3 | CN | 是非常棒的 markdown 编辑器/预览工具，就是最小化之后经常崩溃很难受 |
| 2025-07-28 | 4 | CN | 支持行内公式，比Typora好！！！ 作者能修一下bug吗，刚发现的，就是Win11在一个桌面上打开Typedown编辑文件页面，在另一个桌面上就会显示出文件内容，还能修改内容（ |
| 2025-07-25 | 5 | US | Very good markdown tool 👍 Missing two tings: "split view" and "code editor settings". |
| 2025-07-24 | 4 | US | App crashing when printing: Processor AMD Ryzen AI 9 HX 370 w/ Radeon 890M (2.00 GHz) Installed RAM 32.0 GB (23.6 GB usable) System type 64-bit operating system… |
| 2025-07-13 | 5 | CN | 很高兴看到作者使用 WinUI3 构建。体验非常好 不过有几点，就是深色模式的优化问题：分割线、表格边框和一些控件在深色模式下还是黑色的，可读性很差 |
| 2025-07-09 | 1 | CN | 这个玩意太**了，字数一多就卡，高光都用不了一点，评分这么高都不知道怎么来的，markdown的话==ssss==中间的ssss不应该是高光吗？他用不了一点。 |
| 2025-07-05 | 5 | CN | 之前一直在用chrome浏览器的Markdown Viewer插件和国产的稻壳浏览器查看md格式的文件，中间也尝试过VScode配合插件打开md文件，但总觉得效果差强人意，总是用不太习惯，今天很偶然的机会在微软应用商店发现了这个宝藏软件，Typedown，win11原生的UI设计，简洁高效的菜单，优雅的渲染效果，基础功… |
| 2025-05-29 | 5 | TW | It feels like a native app — both the performance and editing experience are excellent. |
| 2025-04-28 | 2 | CN | 龟速启动，启动一般需要两三分钟没反应 过半天才突然冒出来 |
| 2025-03-26 | 2 | CN | 点击 大纲怎么都没有反应啊，望修复，点击 大纲怎么都没有反应啊，望修复，点击 大纲怎么都没有反应啊，望修复 |
| 2025-03-03 | 5 | US | I needed a markdown viewer and did a quick search in the Microsoft Store and this was one of the first that popped up. I had no expectations regarding it, just … |
| 2025-02-14 | 5 | KR | If you haven't tried it yet, I highly recommend it. It offers excellent accessibility and convenience. I use it for organizing my study notes and planning my de… |
| 2025-01-06 | 5 | CN | 真的挺好的，间接好用，符合win11审美 |
| 2024-12-27 | 5 | CN | 建议侧栏中把文件和大纲分别独立，比如把大纲移到右侧，可以同时在界面显示出来，我们经常需要同时查看文件在哪个位置，且编辑文档的标题大纲，目前每次都需要点击切换很麻烦 |
| 2024-12-17 | 5 | CN | 很棒，非常light_weight，还在持续更新，特意去git点了star，作者加油！ |
| 2024-11-09 | 4 | US | I hope they add tabs in the future like windows notepad did I was going to replace notepad with this app but because of that I won't this is the only down side … |
| 2024-11-09 | 4 | US | I have been a published novelist for over 5 years. Typedown is my go-to product for writing. It is easy to learn and is quick on its 'feet.' Thanks for the prod… |
| 2024-11-03 | 5 | US | As others have said, this beautiful and performant app is totally on par with most paid apps, yet it's free! It's a pleasant surprise to discover this app. Than… |
| 2024-09-24 | 4 | CN | 原生UI，感动，协调好看 免费，感动 好用，感动 总之我很感动 更新：图片插入似乎有bug，设置不能生效。 |
| 2024-09-03 | 5 | HK | 有几个恼人的地方： 撤销动作总是伴随着闪烁、错误的定位，以及不确定的撤销时机； ctrl键似乎对于松开键的判断有些延迟，当你从一个地方复制东西立刻滑倒另一个地方时会错误地缩放界面； 大纲没有做跟主界面的位置对应，导致有时会迷失在巨大的文档中，某些时候点击大纲位置主界面没有响应要在主界面划两下才生效； 刚进入页面时，文件… |
| 2024-08-31 | 5 | CN | 用这个打开.md文件挺方便的 |
| 2024-08-18 | 5 | CN | 看到作者室友的置顶评论，作为用户不得不配合演出，牛逼普拉斯。不管产品是否好用，评论区情绪价值已提供，好玩。哈哈哈哈 |
| 2024-08-04 | 5 | CN | 希望在点击复制后显示已复制或者复制图标短暂的变成小对号，能方便确认是否复制成功 |
| 2024-07-18 | 5 | CN | 标标准准的Markdown编辑器 该有的功能都有 而且适配Win11UI界面清爽 就是启动速度有点慢…… （还有一个小小的Bug：配置好的导出设置会莫名其妙地消失 但是随便再添加一个新配置就好了） |
| 2024-07-13 | 5 | RU | Тут и дизайн лучше всех аналогов и функции markdown'а поддерживаются все до единой. Бонусом есть экспорт в html (а значит и в любой другой rich формат) и в pdf.… |
| 2024-06-30 | 5 | DE | Ich habe eben "Typedown" heruntergeladen und ich muss sagen: Dieser Editor hat es mir reichlich angetan. Er ist in der Windows 11 UI-Oberfläche im Style der Not… |
| 2024-05-21 | 5 | GB | Probably one of the best Markdown editors on Windows, well designed, feature packed and completely free. Highly recommend this application. |
| 2024-05-09 | 5 | DE | Elegant, schnell, kostenlos, volle Markdown-Unterstützung inkl. Tabellen, Aufzählungen, In-Line-Mathematik. WYSIWIG wie Typora. |
| 2024-04-01 | 5 | CN | 看样子是学生做的，做的很好！ |
| 2024-02-20 | 5 | CN | 用上它，从此离开MarkText！ |
| 2024-01-25 | 4 | CN | 图片上传功能点击测试提示NotImplemented. |
| 2024-01-25 | 5 | CN | 期待能够导入css样式修改主题 |
| 2024-01-24 | 4 | CN | 1.当我把一段文字复制过来，typedown会把它识别成代码 2.可能是我不会用，导出的html或pdf在格式排版上多多少少有些问题 3.用了几周后，我电脑上的360安全卫士忽然弹出弹窗说typedown的一个dll文件是病毒，并把它删了？！然后应用就打不开了，我又重装了一次。 |
| 2024-01-16 | 5 | CN | 免费强大方便，特意过来好评的 |
| 2024-01-02 | 5 | US | I don't mind offering more effusive praise, after all, this software totally hits the mark for me in terms of aesthetics and functionality. Outside of elegance … |
| 2023-12-31 | 5 | CN | 体验很好，Typst的绝佳平替！ |
| 2023-12-21 | 1 | CN | 这软件经常死机也就算了，写了半天的文档，刚才又没反应了。问题是死机就死机吧，原来的文件给我覆盖成0K了！！！！！一下午白写了！用这个软件千万要十几分钟备份一次，不是保存，是复制一份！！！ |
| 2023-12-21 | 5 | CN | 安装完第一次之后重启电脑无法显示内容，无法编辑 |
| 2023-12-17 | 5 | CN | 这个软件在丰富一些实用功能就完美了呢 |
| 2023-11-14 | 5 | CN | 刚下下来的时候觉得跟typora外观看着基本一样，风格也差不多，做了一下基本输入等的对比，体验都还不错。 最主要的是，他不要钱啊，不要钱啊。 还有一个国产的推荐的，叫Ficus，想法是不错的，但功能上要差的多。 |
| 2023-10-30 | 1 | CN | 为什么我的typedown打开后没有光标可编辑 直接页面一片白纸，没有输入框 |
| 2023-10-26 | 5 | CN | typora的很好的替代品，不过还有些进步空间。 大纲点击无法跳转，不知是否bug。 |
| 2023-10-23 | 5 | US | Exactly what I was looking for! |
| 2023-10-19 | 5 | HK | 完全满足我这种轻度MD用户的需求，要是由exe安装包就好了，可以修改安装路径。 |
| 2023-09-07 | 4 | CN | 1. 好像不支持通用的文字高亮命令。 ==高亮文字== 2. 插入表格后，在某个单元格内无法输入多行内容。能否参考Excel，按Alt + Enter 在单元格内强行换行？ 谢谢！ |
| 2023-08-31 | 5 | CN | 是我使用方法不对么，点击锚点的超链接不会跳转到对应位置 |
| 2023-08-18 | 4 | CN | Access to the path 'C\user\yonghu\AppData\Local\Temp\529f6d7-1209-4dee-8768-8857b2516df3.html'is denied 我重新下载了 上面的错误不再出现了 但是打印的时候会显示执行此项操作时出错 可以打印其他类型的文件 比如html |
| 2023-08-15 | 5 | CN | 非常不错，完全可以替代typora |
| 2023-08-14 | 5 | CN | 希望能有好看的主题，这是计划的一部分👍 |
| 2023-08-13 | 4 | GB | I love this app and I love it's user interface, but I can tell it's based on Mark Text. The app is quite heavy and resource intensive, and takes quite a bit to … |
| 2023-08-05 | 4 | US | I never remember the name of the app, so I start typing "markdown" in my Windows Start Menu, but your app does not appear as a suggestion. To get this to happen… |
| 2023-07-28 | 5 | CN | 界面用的是Windows原生UI吧，看起来比Typora更舒服没有割裂感，基本功能也全都有，软件还是免费的，作者很厉害，加油 |
| 2023-07-14 | 4 | CN | 遇到个BUG，对代码块的复制粘贴好像有问题，我在Ctrl+A全选所有文本然后复制到新markdown(都在typedown中打开)后发现新markdown的代码块不能正常识别。 还有一个情况是当我从代码块内复制一小段文本时发现复制的文本有偏移，比如一整行的文本是 /* If the drawable has no in… |
| 2023-07-13 | 5 | JP | Best simple and light app. |
| 2023-07-05 | 5 | CN | 但希望在代码块中加入行号功能 |
| 2023-07-04 | 5 | CN | 如果能有exe版本就更好了，公司的同事们急需 |
| 2023-06-02 | 5 | CN | 好用也非常漂亮, 特别是win11使用时特别好看. 美中不足的就是500MB占用感觉不太"轻量". 最近在考虑用tauri做个自用的, 但估计没办法能和win11有这么高的集成度... 更新: 作者优化了应用大小, 现在只有100多MB了, 好顶赞. |
| 2023-05-20 | 5 | CN | 作为作者的室友，脖子上架着一把3米8的砍刀，迫不得与写下好评！！好用！无敌！从此不用Office！！！ |
| 2023-05-12 | 4 | CN | 无意中搜到，下来用了感觉非常好 问题： 1、就是增加了锚点的代码会显示出来； 2、导出的pdf文件，要是可以增加导出带目录（大纲）功能就好了； 作者加油，支持！！！支持！！！ |
| 2023-05-09 | 5 | US | I wish it had Right-to-Left support. |
| 2023-05-08 | 5 | CN | 很高兴能够使用这款软件，希望作者加油 |
| 2023-04-17 | 5 | CN | 1、美中不足的是上传图片的设置有点太极客了，希望可以提供一点更加方便的上传方式，比如自动调用 picgo ，或者通过填写图床地址和 token 的方式。 2、新建文件时，希望可以自动根据首行标题自动在文件夹下生成一个 .md 文档，免去每次新建文档还得手动另存为一次。 3、代码是否开源？建议提供一个提 issue 的渠… |
| 2023-04-12 | 5 | US | I love everything about it. Especially the clean minimalist UI. One problem I found was clicking on a hash link to the same page doesn't navigate. Clicking on a… |
| 2023-04-06 | 5 | US | This editor have very good fonts, style, code highlighting, paste images and other functionality. |
| 2023-03-06 | 5 | CN | 有没有支持移动端的计划呢？ |
| 2023-02-12 | 4 | CN | 希望可以调整字体大小调整逻辑，现在正常字体可以变大，但是代码框内的字还是原来的，对于高分屏显示非常不利。 |
| 2023-01-16 | 5 | CN | 第一次发现，有亿点点惊喜 比起typora来说是免费的 可就是差了一些目录等功能 改进一下就更好了 谢谢开发者❤ |
| 2022-12-28 | 5 | CN | （12/28/2022）偶有闪退现象，希望能尽快修复 |
| 2022-12-06 | 4 | CN | 导出和打印功能无法正常使用 |
| 2022-11-04 | 4 | CN | 提示ctrl+左键跳转，实际并未实现跳转，希望改进 |
| 2022-11-01 | 5 | CN | 虽然功能还有待完善， 但是目前已经够应付大多数场景， 为开发者打Call; 可以安心卸载某个收费的软件了, 当然我不排斥收费， 但是好歹基础功能别强制收费。 |
| 2022-10-31 | 5 | HK | 自从typora收费后，就一直在找寻与之功能相同的软件。试用过一些软件后，还能未能满足我的需求。猛然间在store中看见此款软件，打开使用过一段时间，真心不错！ |
| 2022-10-30 | 5 | US | Good markdown editor. Tried a bunch of apps, and finally deicide to keep this one |
| 2022-10-13 | 2 | RU | links doesn't open. poor crapware. |
| 2022-10-12 | 2 | CN | 希望可以处理一下不太稳定的问题typora |
| 2022-10-07 | 4 | CN | 语法高亮还是很多敏感词没有标注，建议提升这方面 |
| 2022-09-30 | 5 | CN | UI风格非常惊艳。目前发现一点小问题：yaml fronter matter 块渲染出来比较奇怪 |
| 2022-09-12 | 4 | CN | 软件很不错，和Typora体验相近。 一个小问题：无法使用键盘Ctrl+方向键选中段落，只能使用鼠标选中，希望作者改善下。 |
| 2022-09-06 | 5 | CN | 相比于其他typora、marktext、obsidian、思源之类的编辑器，这款简直就是win11自带记事本的升级markdown版本，良心软件，简介而优雅，对表格还有优化，爱了爱了，不管颜值功能都无敌。 |
| 2022-08-01 | 5 | CN | 在1500字左右会有小概率闪退，但是整体流畅度还好，也有自动保存功能，影响不是很大。希望后续继续优化。 |
| 2022-07-27 | 5 | CN | 希望可以增加双栏模式（源码/预览）。 阅读模式。 |
| 2022-07-20 | 5 | CN | md里面图片是显示的，但是PDF中图片就看不到 |
| 2022-07-18 | 5 | CN | 要是支持 css 格式就更好了 |
| 2022-07-16 | 5 | CN | 功能跟 那个软件一模一样，感觉可以完全替代！好良心的开发者，而且支持好多代码块，基本上vscode的支持的都有（除了汇编的...，不过没啥影响），能插入图表，功能很强大！宝藏软件啊 不过优点缺点：大纲地方如果能折叠就最好啦 |
| 2022-07-12 | 4 | CN | 打开关闭时候会卡住，有没有相同情况的？希望提升运行流畅度 |
| 2022-07-04 | 4 | CN | img标签的width属性是无效的，尝试zoom依然无效。也希望能够自定义CSS主题 |
| 2022-06-29 | 5 | CN | Typedown与系统自带记事本界面几乎相同，遵循了Win11的应用风格，这样的软件值得推荐！ [正在卸载Markdown Pad……] |
| 2022-06-17 | 1 | CN | 最上面的一行,鼠标指上去就变长双箭头,长按动不来 |
| 2022-06-15 | 5 | KR | NICE APP. I was just looking for a markdown editor and was surprised to find a good quality app. 😀 |
| 2022-06-09 | 5 | CN | 例如：$\color{Pink} {我}\color{rgb(0,0,0)} {爱}\color{#008000} {你}$ 中 颜色无法用color{rgb(0,0,0)}表示。 |
| 2022-06-03 | 4 | CN | 与windows系统契合度很高，毛玻璃体验良好。 但是功能还有待完善： 1. 打印/导出 pdf 图片丢失 2. 自定义样式 3. 高亮语法<mark>text</mark>略复杂，建议==text== 4. 标题栏高度略大，建议整合菜单栏一体化 5. 任务列表好评，建议追加快捷键 |
| 2022-06-01 | 5 | CN | 和Typora相比虽然还有很多功能有待完善，但在适用性和易用性上，确实很好 下面我提点在使用过程中遇到的问题吧 1、文件列表不能用，新建的文件，在列表中不显示 2、每次打开软件不能加载上次的笔记 3、保存的笔记文件，不能自动加载到文件列表中，在每次使用时，首先需要找到文件 4、代码块功能希望越来越完善。 |
| 2022-05-26 | 5 | CN | 软件是个好东西，但是这个版本安装不起 |
| 2022-05-23 | 4 | CN | ctrl+F的搜索功能经常没反应 |
| 2022-05-14 | 4 | CN | 在 intel 的电脑上使用不错，可惜 arm64 架构上无法启动。 |
| 2022-05-11 | 3 | GB | Undo and Redo using command keys only works in Source Code Mode. If you paste and then switch modes, what you pasted disappears. |
| 2022-05-07 | 5 | CN | 我觉得体验不错，很舒适，不过我有一个问题，左边的文件和大纲的按钮是干啥用的，我点了毫无反应，如果能实现存储一个文件系列的功能（比如小说的第一章、第二章等等）就好了！希望著作团队能看到。 |
| 2022-04-28 | 5 | CN | 真的是个suprise，基本的功能都有了：缺TOC、缺mermaid图表 |
| 2022-04-27 | 5 | GB | Markdown editor that works well that includes most of the common features. Would like to see export to docx format. |
| 2022-04-07 | 5 | CN | 在编辑器中公式字体是LaTeX的Latin Modern Math，但导出为pdf后字体会变得不同。 |
| 2022-04-05 | 4 | CN | 多个换行合并成一个换行,要用<br>才能多个换行有点麻烦 |
| 2022-04-01 | 5 | CN | 超链接(ctrl+x)会和剪切冲突哦，其他都非常满足需求。 |
| 2022-03-31 | 5 | CN | 图床图片有的会加载不出来，在别的软件上可以快速加载的，麻烦作者检查一下咱们这个软件是什么问题。 |
| 2022-03-31 | 5 | CN | 自从Typora发布正式版后，就开始寻找代替品了，发现这个软件还不错，UI和功能都和Typora类似，符合习惯；但我发现Typedown不能预览网络资源图片，出去这个小问题，还是十分喜欢这个软件的 |
| 2022-03-24 | 4 | CN | ui界面很简洁，风格和windows十分同一使用时没有突兀感就像巨硬原装一样，好评 给四星是因为当前只支持导出为pdf和HTML希望后期加入更多的导出格式如word等，十分期待后续更新！ |
| 2022-03-23 | 5 | CN | 导出为pdf时如果代码段有特别长的一行，会把一个横向的滚动条打印在pdf上，同时这一行超出显示范围的部分也没法显示了 |
| 2022-03-20 | 5 | CN | 体验不错，简洁方便。 希望加入大纲视图标题可折叠功能和多标标签功能。 |
| 2022-03-11 | 5 | RU | Спасибо! наверное это уже лучший редактор WYSIWYG в Microsoft Store. |
| 2022-03-02 | 5 | CN | 偶尔发现的 有点小惊喜，也有点小遗憾[功能支持还不太完善] |

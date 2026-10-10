using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Which cases a run takes when it is asked by selector instead of by name: <c>quick</c>, <c>outline</c>, or several at
/// once with names (<c>quick,menus,DG01</c>; <c>@quick</c> works too when the driver is started directly). Every case has a tier - quick (often broken before, or in what
/// the WinUI 3 move keeps breaking: focus and keys, window chrome, outline, themes, menus) or full only - and the areas
/// it touches. A fix runs @quick and the areas of what it changed; a push for CI, a package or a release runs everything.
/// </summary>
internal static partial class CaseCatalog
{
    private sealed record Entry(bool Quick, string[] Areas);

    /// <summary>An edition's own cases with their tiers, areas and purposes (Edition.&lt;name&gt;.cs, in the edition's repository).</summary>
    static partial void EditionCatalog(Dictionary<string, Entry> cases, Dictionary<string, string> purposes);

    static CaseCatalog() => EditionCatalog(Cases, Purposes);

    private static Entry Q(params string[] areas) => new(true, areas);

    private static Entry F(params string[] areas) => new(false, areas);

    // The areas: api (the automation API), keys (focus and the keyboard), outline, window (full screen, minimize, DPI,
    // the frame), theme, menus, files (tabs, backups, the file tree, links), paste (web pages and pictures in), export,
    // content (what the editor keeps of a document), perf (measurements: only when named), edition (an edition's own).
    private static readonly Dictionary<string, Entry> Cases = new()
    {
        ["S0"] = F("api"),
        ["S1"] = Q("api", "keys"),
        ["R01"] = F("api"),
        ["R02"] = F("api"),
        ["R03"] = Q("api", "keys"),
        ["B01"] = F("api"),
        ["S2"] = F("api"),
        ["S3"] = Q("api", "menus"),
        ["V01"] = F("api", "window"),
        ["N01"] = F("api", "files"),
        ["K01"] = F("api", "keys"),
        ["B02"] = F("api"),
        ["F01"] = Q("api", "content", "keys"),
        ["M01"] = F("api", "content"),
        ["P01"] = F("api", "window"),
        ["W01"] = F("api"),
        ["W02"] = F("api"),
        ["W03"] = F("api"),
        ["MC01"] = F("api"),
        ["EX01"] = F("api"),
        ["EQ01"] = F("api"),
        ["R04"] = Q("files", "api"),
        ["C01"] = Q("api", "files"),
        ["Q02"] = F("window"),
        ["D01"] = F("api"),
        ["K02"] = Q("keys", "menus"),
        ["K04"] = Q("keys"),
        ["K05"] = Q("keys", "files"),
        ["K06"] = Q("keys", "menus", "outline"),
        ["K03"] = F("keys", "menus"),
        ["FS01"] = Q("window"),
        ["FS03"] = Q("window", "menus"),
        ["FS02"] = Q("window"),
        ["PU01"] = F("content", "export"),
        ["RV01"] = F("api"),
        ["IU01"] = F("paste", "menus"),
        ["IU03"] = F("paste"),
        ["IU02"] = F("paste"),
        ["IU04"] = F("paste"),
        ["IN01"] = F("paste", "files"),
        ["VI01"] = F("keys"),
        ["VI02"] = F("keys"),
        ["TC01"] = Q("outline", "files"),
        ["TC02"] = Q("outline", "files"),
        ["SE01"] = F("files"),
        ["WP02"] = F("paste"),
        ["EX02"] = F("export"),
        ["TB01"] = Q("content", "menus"),
        ["TH01"] = Q("theme"),
        ["RD01"] = Q("keys", "menus", "content"),
        ["CP01"] = F("export", "paste"),
        ["TH03"] = Q("theme", "outline"),
        ["ES01"] = Q("keys", "menus"),
        ["ES02"] = Q("keys"),
        ["TI01"] = F("window"),
        ["TE01"] = F("window"),
        ["MN01"] = F("window", "api"),
        ["LK01"] = Q("files"),
        ["FL01"] = F("menus"),
        ["FO01"] = Q("keys", "menus"),
        ["TH04"] = F("theme", "window"),
        ["FT01"] = F("files"),
        ["MN02"] = Q("window", "keys"),
        ["TH05"] = Q("theme"),
        // Fails as a known issue (restored without activation, the editor has no size): in full runs only.
        ["MN03"] = F("window"),
        ["MN04"] = Q("window"),
        ["MN05"] = F("window", "files"),
        ["DP01"] = Q("window"),
        ["DR01"] = Q("files", "paste"),
        ["DM01"] = Q("theme", "window"),
        ["TH02"] = F("theme", "menus"),
        ["WP01"] = Q("paste"),
        ["ER01"] = F("api"),
        ["DG01"] = Q("theme", "menus"),
        ["PM01"] = Q("menus"),
        ["OL02"] = Q("outline"),
        ["OL01"] = Q("outline"),
        ["LD01"] = F("content", "files"),
        ["PF01"] = F("perf"),
        ["PF03"] = F("perf"),
        ["PF02"] = F("perf"),
        ["Q01"] = F("window"),
    };

    // What each case checks, in a few words, shown in the test window's title while it runs (with its name), so whoever
    // watches the screen can tell what should be happening.
    private static readonly Dictionary<string, string> Purposes = new()
    {
        ["S0"] = "打开、写入、读回、保存逐字节一致",
        ["S1"] = "真实按键算编辑，修订号前进",
        ["R01"] = "切换标签时挂起的写入不写错文档",
        ["R02"] = "撤销重做只作用于各自文档",
        ["R03"] = "保存期间的按键被保留",
        ["B01"] = "一个窗口改设置，其他窗口同步",
        ["S2"] = "接口改设置到达所有窗口和文件",
        ["S3"] = "语言、缩进、紧凑模式、字数设置生效",
        ["V01"] = "切换源码/阅读模式与窗口大小",
        ["N01"] = "从设置页返回后立即读写文档",
        ["K01"] = "接口改字号时按键仍能输入",
        ["B02"] = "设置页显示别处改的字号",
        ["F01"] = "特殊内容经首次按键不丢失",
        ["M01"] = "三种模式来回切换文字不变",
        ["P01"] = "最小化窗口后台标签写入后绘制",
        ["W01"] = "写入后页面重载，主机与页面一致",
        ["W02"] = "写入挂起时切模式，写入生效",
        ["W03"] = "写入挂起时切走标签再切回",
        ["MC01"] = "命令行 MCP 服务读写与冲突",
        ["EX01"] = "PowerShell/Python 示例客户端",
        ["EQ01"] = "与 Uno 版的等价性场景",
        ["R04"] = "强制结束后未命名和后台文档从备份恢复",
        ["C01"] = "关闭文档：已保存可关，未保存拒绝",
        ["Q02"] = "窗口刚打开就关，进程不退出",
        ["D01"] = "界面线程执行多线程投递的回调",
        ["K02"] = "两次 Ctrl+, 开关设置后光标键盘还在",
        ["K04"] = "新窗口不点击就能打字",
        ["K05"] = "新标签和点击标签后直接能打字",
        ["K06"] = "关标签、菜单、模式切换等之后仍能打字",
        ["K03"] = "未命名文档的同样检查",
        ["FS01"] = "全屏时页面贴着屏幕顶边",
        ["FS03"] = "全屏时鼠标到顶部显示菜单、移开收起",
        ["FS02"] = "退出全屏后标题栏能立即拖动",
        ["PU01"] = "PlantUML 默认不联网，开启后才画",
        ["RV01"] = "定位到修改处的滚动行为",
        ["IU01"] = "上传本地图片（PowerShell）",
        ["IU03"] = "新窗口立即读到上传配置",
        ["IU02"] = "上传图片到 S3",
        ["IU04"] = "粘贴截图和图片的上传文件名",
        ["IN01"] = "一次拖入多张图片",
        ["VI01"] = "源码模式的 Vim 按键",
        ["VI02"] = "阅读模式的 Vim 滚动按键",
        ["TC01"] = "关闭标签后大纲显示下一个标签",
        ["TC02"] = "关闭非相邻标签后大纲正确",
        ["SE01"] = "启动行为设置写入设置文件",
        ["WP02"] = "粘贴网页图片的防盗链和失败处理",
        ["EX02"] = "导出完成提示与打开按钮",
        ["TB01"] = "表格工具栏调整表格大小",
        ["TH01"] = "自定义主题在三种模式下生效",
        ["RD01"] = "阅读模式右键菜单只能复制",
        ["CP01"] = "复制到 Word 的图片和格式",
        ["TH03"] = "侧栏选中标记用主题强调色",
        ["ES01"] = "输入时编辑器快捷键（Ctrl+1）有效",
        ["ES02"] = "Ctrl+B 加粗后保持",
        ["TI01"] = "窗口带应用图标",
        ["TE01"] = "还原窗口可从顶边和角拖动调整",
        ["MN01"] = "最小化窗口经接口写入和保存",
        ["LK01"] = "点 md 链接在同窗口新标签打开",
        ["FL01"] = "设置里字体框列出已装字体",
        ["FO01"] = "切窗口和关右键菜单后能直接打字",
        ["TH04"] = "系统切深浅色时标题按钮和编辑区跟随",
        ["FT01"] = "文件树排序与 1.3.7 一致",
        ["MN02"] = "最小化后恢复，文档立即显示并能输入",
        ["TH05"] = "编辑区跟随深色/黑色/云母开关/浅色",
        ["MN03"] = "最小化时写入，未激活恢复后编辑区尺寸",
        ["MN04"] = "全屏进出后最小化再恢复，窗口正常",
        ["MN05"] = "全屏后最小化，从资源管理器打开文件",
        ["DP01"] = "高缩放下不模糊（DPI 感知）",
        ["DR01"] = "从资源管理器拖入文件和图片",
        ["DM01"] = "窗口顶边和对话框遮罩跟随主题",
        ["TH02"] = "重新加载主题能发现新文件",
        ["WP01"] = "粘贴网页转成整洁的 Markdown",
        ["ER01"] = "页面消息处理出错不影响其他",
        ["DG01"] = "对话框使用自定义主题配色",
        ["PM01"] = "段落菜单勾选跟随光标位置",
        ["OL02"] = "阅读模式切标签时大纲不闪跳",
        ["OL01"] = "快速切换标签大纲保持展开",
        ["LD01"] = "编辑器改写内容时只重载一次",
        ["PF01"] = "性能：打开、输入、保存 300KB",
        ["PF03"] = "性能：打开大文档的 CPU 分布",
        ["PF02"] = "性能：冷启动和关闭",
        ["Q01"] = "两个窗口依次关闭后进程退出",
    };

    /// <summary>"MN02 最小化后恢复，文档立即显示并能输入": the case's name and what it checks, or null.</summary>
    public static string? Label(string? caseId) =>
        caseId == null ? null : Purposes.TryGetValue(caseId, out var purpose) ? $"{caseId} {purpose}" : caseId;

    public static IEnumerable<string> Areas => Cases.Values.SelectMany(e => e.Areas).Distinct().OrderBy(a => a);

    /// <summary>
    /// The names a run takes from <paramref name="selection"/>: case names as they are, @quick, @all (everything,
    /// null), or @&lt;area&gt;. An unknown selector is an error that lists the known ones.
    /// </summary>
    public static string[]? Expand(string[] selection)
    {
        var names = new List<string>();
        foreach (var item in selection.Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            // A selector is @word or a word in lower case (case names are capitals and digits): the scripts hand the
            // list on through PowerShell, where a word starting with @ is splatting and the list arrived empty.
            var isSelector = item.StartsWith("@") || item.All(ch => ch >= 'a' && ch <= 'z');
            if (!isSelector) { names.Add(item); continue; }
            var selector = item.TrimStart('@').ToLowerInvariant();
            if (selector == "all") return null;
            var chosen = selector == "quick"
                ? Cases.Where(c => c.Value.Quick).Select(c => c.Key).ToList()
                : Cases.Where(c => c.Value.Areas.Contains(selector)).Select(c => c.Key).ToList();
            if (chosen.Count == 0)
                throw new ArgumentException($"unknown selector {item}: @quick, @all, or an area ({string.Join(", ", Areas.Select(a => "@" + a))})");
            names.AddRange(chosen);
        }
        return names.Distinct().ToArray();
    }

    /// <summary>
    /// A case with both its tier and its purpose here. One without runs in full runs, but no selector finds it and the
    /// test window's title cannot say what it checks: the driver notes it.
    /// </summary>
    public static bool Knows(string caseName)
    {
        var id = caseName.Split(' ')[0];
        return Cases.ContainsKey(id) && Purposes.ContainsKey(id);
    }
}

# Writes the themes that ship with the app. One palette per theme; the CSS is generated so the alpha ramps
# always match the theme's own text colour instead of being copied by hand.
import os

def rgba(hexcolor, a):
    h = hexcolor.lstrip('#')
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    return f"rgba({r}, {g}, {b}, {a})"

THEMES = [
    dict(id="solarized-light", name="Solarized Light", base="light", author="Typedown",
         bg="#fdf6e3", fg="#073642", surface="#eee8d5", border="#e0dbc8", accent="#268bd2",
         code="#eee8d5", highlight="#b58900", delete="#dc322f", icon="#657b83",
         tokens=dict(comment="#93a1a1", keyword="#859900", string="#2aa198", number="#d33682",
                     function="#268bd2", operator="#657b83", tag="#268bd2"), italic_comment=True),
    dict(id="sepia", name="Sepia", base="light", author="Typedown",
         bg="#f4ecd8", fg="#4b3f2f", surface="#ece2c8", border="#ddd0b0", accent="#a1683a",
         code="#eae0c8", highlight="#c8a03c", delete="#a33a2a", icon="#7a6a54",
         tokens=dict(comment="#8a7d68", keyword="#8f5a2b", string="#6a7f3a", number="#a1683a",
                     function="#7a5c2e", operator="#7a6a54", tag="#8f5a2b"), italic_comment=True),
    dict(id="nord", name="Nord", base="dark", author="Typedown",
         bg="#2e3440", fg="#d8dee9", surface="#3b4252", border="#4c566a", accent="#88c0d0",
         code="#3b4252", highlight="#ebcb8b", delete="#bf616a", icon="#8fbcbb",
         tokens=dict(comment="#616e88", keyword="#81a1c1", string="#a3be8c", number="#b48ead",
                     function="#88c0d0", operator="#81a1c1", tag="#8fbcbb"), italic_comment=True),
    dict(id="solarized-dark", name="Solarized Dark", base="dark", author="Typedown",
         bg="#002b36", fg="#93a1a1", surface="#073642", border="#0f4a58", accent="#268bd2",
         code="#073642", highlight="#b58900", delete="#dc322f", icon="#93a1a1",
         tokens=dict(comment="#586e75", keyword="#859900", string="#2aa198", number="#d33682",
                     function="#268bd2", operator="#93a1a1", tag="#268bd2"), italic_comment=True),
    dict(id="dracula", name="Dracula", base="dark", author="Typedown",
         bg="#282a36", fg="#f8f8f2", surface="#343746", border="#44475a", accent="#bd93f9",
         code="#343746", highlight="#f1fa8c", delete="#ff5555", icon="#8be9fd",
         tokens=dict(comment="#6272a4", keyword="#ff79c6", string="#f1fa8c", number="#bd93f9",
                     function="#50fa7b", operator="#ff79c6", tag="#ff79c6"), italic_comment=True),
    dict(id="gruvbox-dark", name="Gruvbox Dark", base="dark", author="Typedown",
         bg="#282828", fg="#ebdbb2", surface="#32302f", border="#504945", accent="#fabd2f",
         code="#32302f", highlight="#d79921", delete="#fb4934", icon="#d5c4a1",
         tokens=dict(comment="#928374", keyword="#fb4934", string="#b8bb26", number="#d3869b",
                     function="#8ec07c", operator="#fe8019", tag="#fabd2f"), italic_comment=True),
]

TEMPLATE = """/* Typedown theme
 * name: {name}
 * base: {base}
 * accent: {accent}
 * author: {author}
 *
 * Shipped with Typedown. Copy this file into the themes folder and edit it there to make it your own: a file
 * of your own with the same name takes the place of this one. See docs/custom-theme.md for the format.
 * background: {bg}
 * surface: {surface}
 * foreground: {fg}
 * border: {border}
 */
:root {{
  --editorBgColor: {bg};
  --editorColor: {fg};
  --editorColor80: {fg80};
  --editorColor60: {fg60};
  --editorColor50: {fg50};
  --editorColor40: {fg40};
  --editorColor30: {fg30};
  --editorColor10: {fg10};
  --editorColor04: {fg04};
  --themeColor: {accent};
  --selectionColor: {sel};
  --highlightColor: {hl};
  --codeBgColor: {code};
  --codeBlockBgColor: {code};
  --footnoteBgColor: {fg04};
  --inputBgColor: {fg10};
  --tableBorderColor: {border};
  --itemBgColor: {code};
  --floatBgColor: {float_bg};
  --floatHoverColor: {fg10};
  --floatBorderColor: {fg30};
  --floatShadow: 0 2px 12px {shadow};
  --maskColor: {mask};
  --iconColor: {icon};
  --deleteColor: {delete};
}}

/* Code blocks (Prism) */
.token.comment, .token.prolog, .token.doctype, .token.cdata {{ color: {t_comment};{comment_style} }}
.token.keyword, .token.atrule, .token.important {{ color: {t_keyword}; }}
.token.string, .token.char, .token.attr-value, .token.regex {{ color: {t_string}; }}
.token.number, .token.boolean, .token.constant, .token.symbol {{ color: {t_number}; }}
.token.function, .token.class-name {{ color: {t_function}; }}
.token.operator, .token.punctuation, .token.entity, .token.url {{ color: {t_operator}; }}
.token.tag, .token.selector, .token.attr-name, .token.property {{ color: {t_tag}; }}

/* Source mode (CodeMirror) */
.CodeMirror {{ background: {bg}; color: {fg}; }}
.CodeMirror-cursor {{ border-left-color: {fg}; }}
.CodeMirror .cm-comment {{ color: {t_comment}; }}
.CodeMirror .cm-header {{ color: {accent}; }}
.CodeMirror .cm-link, .CodeMirror .cm-url {{ color: {t_function}; }}
.CodeMirror .cm-string {{ color: {t_string}; }}
.CodeMirror .cm-keyword {{ color: {t_keyword}; }}
.CodeMirror .cm-quote {{ color: {fg60}; }}
"""

def render(t):
    fg, accent = t["fg"], t["accent"]
    return TEMPLATE.format(
        name=t["name"], base=t["base"], accent=accent, author=t["author"],
        bg=t["bg"], surface=t["surface"], fg=fg, border=t["border"],
        fg80=rgba(fg, ".8"), fg60=rgba(fg, ".6"), fg50=rgba(fg, ".5"), fg40=rgba(fg, ".4"),
        fg30=rgba(fg, ".3"), fg10=rgba(fg, ".1"), fg04=rgba(fg, ".04"),
        sel=rgba(accent, ".22"), hl=rgba(t["highlight"], ".35"), code=t["code"],
        float_bg=t["surface"] if t["base"] != "light" else t["bg"],
        shadow=rgba("#000000", ".35" if t["base"] != "light" else ".12"),
        mask=rgba(t["bg"], ".8"), icon=t["icon"], delete=t["delete"],
        t_comment=t["tokens"]["comment"], t_keyword=t["tokens"]["keyword"], t_string=t["tokens"]["string"],
        t_number=t["tokens"]["number"], t_function=t["tokens"]["function"],
        t_operator=t["tokens"]["operator"], t_tag=t["tokens"]["tag"],
        comment_style=" font-style: italic;" if t["italic_comment"] else "")

# Both editions ship the same files; pass the folders to write, or let it write the two in this checkout.
import sys
folders = sys.argv[1:] or [os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                                        "Dev", "Typedown", "Resources", "Themes")]
for folder in folders:
    os.makedirs(folder, exist_ok=True)
    for t in THEMES:
        open(os.path.join(folder, t["id"] + ".css"), "w", encoding="utf-8").write(render(t))
    print(folder, sorted(os.listdir(folder)))

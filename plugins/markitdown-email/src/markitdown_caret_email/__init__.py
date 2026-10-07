"""MarkItDown plugin: Outlook and .eml emails to clean, redacted Markdown threads.

Enable it with ``markitdown --use-plugins mail.msg`` or ``MarkItDown(enable_plugins=True)``,
or use the ``caret-email`` command, which needs no plugin setup.
"""

from markitdown import MarkItDown

from ._converter import EmailConverter, Options, render
from ._redact import Redactor
from ._rules import load_rules

__version__ = "0.2.0"

# The MarkItDown plugin interface this plugin was written against.
__plugin_interface_version__ = 1

# Just ahead of the built-in converters, so this one handles .msg instead of the
# basic built-in Outlook converter.
PRIORITY = -1.0

__all__ = ["EmailConverter", "Options", "Redactor", "load_rules", "register_converters", "render"]


def register_converters(markitdown: MarkItDown, **kwargs) -> None:
    """Called by MarkItDown when plugins are enabled."""
    markitdown.register_converter(EmailConverter(markitdown), priority=PRIORITY)

"""Read-only Windows UI Automation MCP server.

Only enumerates visible windows and UI Automation metadata. It does not click,
type, submit forms, alter application state, or connect to databases.
"""
from __future__ import annotations

import json
import logging
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from mcp.server.fastmcp import FastMCP

# MCP uses stdout for its protocol. Log only to stderr.
logging.basicConfig(stream=sys.stderr, level=logging.INFO)
logger = logging.getLogger("windows-ui-inspector")
mcp = FastMCP("windows-ui-inspector")

if os.name != "nt":
    # The server can import on non-Windows hosts, which makes error messages
    # clearer. Actual inspection is supported only on Windows.
    WINDOWS_AVAILABLE = False
else:
    WINDOWS_AVAILABLE = True


def _require_windows() -> None:
    if not WINDOWS_AVAILABLE:
        raise RuntimeError("Este servidor solo puede inspeccionar la interfaz de Windows y debe ejecutarse en el equipo Windows donde está abierta la aplicación.")


def _desktop():
    _require_windows()
    from pywinauto import Desktop
    return Desktop(backend="uia")


def _safe_get(obj: Any, getter: str, default: Any = None) -> Any:
    try:
        value = getattr(obj, getter)
        value = value() if callable(value) else value
        return value if value is not None else default
    except Exception:
        return default


def _rect(control: Any) -> dict[str, int] | None:
    try:
        r = control.rectangle()
        return {"left": int(r.left), "top": int(r.top), "right": int(r.right), "bottom": int(r.bottom), "width": int(r.width()), "height": int(r.height())}
    except Exception:
        return None


def _describe_control(control: Any, depth: int = 0) -> dict[str, Any]:
    info = _safe_get(control, "element_info")
    handle = _safe_get(control, "handle")
    return {
        "handle": int(handle) if isinstance(handle, int) and handle else None,
        "process_id": _safe_get(info, "process_id"),
        "name": _safe_get(info, "name", "") or "",
        "automation_id": _safe_get(info, "automation_id", "") or "",
        "control_type": _safe_get(info, "control_type", "") or "",
        "class_name": _safe_get(info, "class_name", "") or "",
        "framework_id": _safe_get(info, "framework_id", "") or "",
        "is_enabled": _safe_get(control, "is_enabled"),
        "is_visible": _safe_get(control, "is_visible"),
        "has_keyboard_focus": _safe_get(control, "has_keyboard_focus"),
        "rectangle": _rect(control),
        "depth": depth,
    }


def _window_handle(window_handle: int):
    if not isinstance(window_handle, int) or window_handle <= 0:
        raise ValueError("window_handle debe ser un entero positivo obtenido de list_windows o get_active_window.")
    desktop = _desktop()
    try:
        return desktop.window(handle=window_handle).wrapper_object()
    except Exception as exc:
        raise RuntimeError(f"No se pudo obtener la ventana con handle {window_handle}. Confirma que siga abierta y que el handle provenga de este equipo. Detalle: {exc}") from exc


def _build_tree(window_handle: int, max_depth: int, max_nodes: int) -> dict[str, Any]:
    root = _window_handle(window_handle)
    root_node = _describe_control(root, depth=0)
    nodes_seen = 1
    truncated = False

    def add_children(parent: Any, parent_node: dict[str, Any], depth: int) -> None:
        nonlocal nodes_seen, truncated
        if depth >= max_depth:
            try:
                if parent.children():
                    parent_node["children_truncated_by_depth"] = True
            except Exception:
                pass
            return
        try:
            children = parent.children()
        except Exception as exc:
            parent_node["children_error"] = str(exc)[:300]
            return
        parent_node["children"] = []
        for child in children:
            if nodes_seen >= max_nodes:
                truncated = True
                parent_node["children_truncated_by_node_limit"] = True
                break
            node = _describe_control(child, depth=depth + 1)
            parent_node["children"].append(node)
            nodes_seen += 1
            add_children(child, node, depth + 1)

    add_children(root, root_node, 0)
    return {
        "captured_at_utc": datetime.now(timezone.utc).isoformat(),
        "backend": "pywinauto UI Automation (uia)",
        "read_only": True,
        "window_handle": window_handle,
        "limits": {"max_depth": max_depth, "max_nodes": max_nodes},
        "nodes_returned": nodes_seen,
        "truncated": truncated,
        "tree": root_node,
        "privacy_note": "El árbol incluye nombres accesibles y metadatos de controles, pero no lee explícitamente el valor de los campos Edit. Revisa los nombres antes de compartir el informe porque algunas aplicaciones pueden exponer texto sensible como nombre accesible.",
    }


@mcp.tool()
def list_windows(visible_only: bool = True) -> str:
    """Lista ventanas de nivel superior y devuelve título, handle, PID y metadatos básicos. No interactúa con ellas."""
    desktop = _desktop()
    results: list[dict[str, Any]] = []
    errors: list[str] = []
    try:
        windows = desktop.windows()
    except Exception as exc:
        return json.dumps({"error": f"No se pudieron enumerar las ventanas: {exc}"}, ensure_ascii=False)

    for window in windows:
        try:
            visible = bool(window.is_visible())
            if visible_only and not visible:
                continue
            item = _describe_control(window, depth=0)
            item["title"] = _safe_get(window, "window_text", "") or item["name"]
            item["is_visible"] = visible
            results.append(item)
        except Exception as exc:
            errors.append(str(exc)[:200])

    results.sort(key=lambda x: (not bool(x.get("is_visible")), str(x.get("title", "")).casefold(), x.get("handle") or 0))
    return json.dumps({"read_only": True, "backend": "uia", "visible_only": visible_only, "count": len(results), "windows": results, "enumeration_errors": errors[:10]}, ensure_ascii=False, indent=2)


@mcp.tool()
def get_active_window() -> str:
    """Devuelve metadatos de la ventana que actualmente tiene el foco, sin realizar acciones."""
    _require_windows()
    try:
        import win32gui
        hwnd = int(win32gui.GetForegroundWindow())
        if hwnd <= 0:
            return json.dumps({"error": "Windows no reportó una ventana activa."}, ensure_ascii=False)
        title = win32gui.GetWindowText(hwnd)
        _, pid = win32gui.GetWindowThreadProcessId(hwnd)
        result: dict[str, Any] = {"handle": hwnd, "title": title, "process_id": int(pid), "read_only": True}
        try:
            result["uia"] = _describe_control(_window_handle(hwnd), depth=0)
        except Exception as exc:
            result["uia_error"] = str(exc)[:500]
        return json.dumps(result, ensure_ascii=False, indent=2)
    except Exception as exc:
        return json.dumps({"error": f"No se pudo consultar la ventana activa: {exc}"}, ensure_ascii=False)


@mcp.tool()
def inspect_window(window_handle: int, max_depth: int = 4, max_nodes: int = 250) -> str:
    """Inspecciona recursivamente el árbol UI Automation de una ventana concreta. Solo lee metadatos; nunca hace clic ni lee explícitamente valores de campos."""
    if max_depth < 1 or max_depth > 8:
        return json.dumps({"error": "max_depth debe estar entre 1 y 8."}, ensure_ascii=False)
    if max_nodes < 10 or max_nodes > 1000:
        return json.dumps({"error": "max_nodes debe estar entre 10 y 1000."}, ensure_ascii=False)
    try:
        return json.dumps(_build_tree(window_handle, max_depth, max_nodes), ensure_ascii=False, indent=2)
    except Exception as exc:
        return json.dumps({"error": str(exc)}, ensure_ascii=False, indent=2)


@mcp.tool()
def save_window_inspection(window_handle: int, max_depth: int = 4, max_nodes: int = 250) -> str:
    """Guarda un árbol UI Automation JSON en evidence/ui-inspection dentro del repositorio. Solo escribe un archivo nuevo; no modifica la aplicación inspeccionada."""
    if max_depth < 1 or max_depth > 8:
        return json.dumps({"error": "max_depth debe estar entre 1 y 8."}, ensure_ascii=False)
    if max_nodes < 10 or max_nodes > 1000:
        return json.dumps({"error": "max_nodes debe estar entre 10 y 1000."}, ensure_ascii=False)
    try:
        report = _build_tree(window_handle, max_depth, max_nodes)
        repo_root_env = os.environ.get("WINDOWS_INSPECTOR_WORKSPACE")
        root = Path(repo_root_env).resolve() if repo_root_env else Path(__file__).resolve().parents[2]
        evidence_dir = (root / "evidence" / "ui-inspection").resolve()
        expected_parent = (root / "evidence").resolve()
        if evidence_dir.parent != expected_parent:
            raise RuntimeError("Ruta de evidencia fuera del repositorio; operación cancelada.")
        evidence_dir.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        out_path = evidence_dir / f"window-{window_handle}-{stamp}.json"
        out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        return json.dumps({"saved": True, "path": str(out_path), "nodes_returned": report["nodes_returned"], "truncated": report["truncated"], "note": "Revisa el archivo por posibles nombres accesibles sensibles antes de compartirlo."}, ensure_ascii=False, indent=2)
    except Exception as exc:
        return json.dumps({"saved": False, "error": str(exc)}, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    mcp.run()

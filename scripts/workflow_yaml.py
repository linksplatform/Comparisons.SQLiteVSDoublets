"""Read workflow YAML safely while preserving GitHub's `on` event key."""

from typing import Any

import yaml


def load_workflow(content: str) -> dict[str, Any]:
    workflow = yaml.safe_load(content)
    if not isinstance(workflow, dict):
        raise ValueError("Workflow must be a mapping")
    # PyYAML's YAML 1.1 resolver treats an unquoted `on` key as boolean true.
    if True in workflow:
        workflow["on"] = workflow.pop(True)
    return workflow

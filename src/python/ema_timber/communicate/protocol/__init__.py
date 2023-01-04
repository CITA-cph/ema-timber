import os
import sys

modules = {}

import pkgutil

for loader, module_name, is_pkg in pkgutil.walk_packages(__path__):
    if is_pkg:
        _module = loader.find_module(module_name).load_module(module_name)
        globals()[module_name] = _module
        modules[module_name] = getattr(_module, module_name, None)
        print(f"{module_name} -- OK")


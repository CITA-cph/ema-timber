import os
import pkgutil

def import_ema_timber():
    current = os.path.dirname(__file__)
    core = os.path.abspath(os.path.join(current ,"../..","src/python/"))

    print(f"We are in {core}")

    for loader, module_name, is_pkg in pkgutil.walk_packages([core]):
        print(f"Found this guy: {module_name}")
        if module_name == "ema_timber":
            _module = loader.find_module(module_name).load_module(module_name)
            globals()[module_name] = _module
            print(f"Found {module_name} in {core}")
            break
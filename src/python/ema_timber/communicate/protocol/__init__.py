modules = {}

import pkgutil

print("\nImporting modules:")
for loader, module_name, is_pkg in pkgutil.walk_packages(__path__):
    if is_pkg:
        print(f"\t{module_name} ", end="")
        try:
            _module = loader.find_module(module_name).load_module(module_name)
            globals()[module_name] = _module
            modules[module_name] = getattr(_module, module_name, None)
            print("-- OK")
        except Exception as e:
            print("-- failed")
            print("\t -",e)
print("Import done")
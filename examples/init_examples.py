import os
import sys

'''
Add the ema_timber folder to system path
so that we can load ema_timber from our
example scripts.
'''

this_dir = os.path.dirname(os.path.abspath(__file__))
base_dir = os.path.dirname(this_dir)
tmp_dir = os.path.join(base_dir, "tmp")

sys.path.append(os.path.join(base_dir, "src", "python"))
"""Build a Windows VERSIONINFO resource with user-visible version 1.0."""
from pathlib import Path
import struct

def align(data):
    return data + b'\0' * (-len(data) % 4)

def block(key, value=b'', children=(), text=False):
    header = struct.pack('<HHH', 0, len(value)//2 if text else len(value), int(text))
    data = align(header + (key+'\0').encode('utf-16le')) + value
    for child in children:
        data = align(data) + child
    return struct.pack('<H', len(data)) + data[2:]

strings = {
    'CompanyName': 'Kuma',
    'FileDescription': 'Kuma亚马逊运费计算器',
    'FileVersion': '1.0',
    'InternalName': 'KumaAmazonCalculator',
    'LegalCopyright': 'Kuma',
    'OriginalFilename': 'Kuma亚马逊运费计算器.exe',
    'ProductName': 'Kuma亚马逊运费计算器',
    'ProductVersion': '1.0',
}
table = block('080404b0', children=[block(k,(v+'\0').encode('utf-16le'),text=True) for k,v in strings.items()], text=True)
string_info = block('StringFileInfo', children=[table], text=True)
translation = block('VarFileInfo', children=[block('Translation',struct.pack('<HH',0x804,1200))], text=True)
fixed = struct.pack('<13I', 0xFEEF04BD,0x10000,1<<16,0,1<<16,0,0x3f,0,0x40004,1,0,0,0)
version = block('VS_VERSION_INFO',fixed,[string_info,translation])
null_header = struct.pack('<IIHHHHIHHII',0,32,0xffff,0,0xffff,0,0,0,0,0,0)
header = struct.pack('<IIHHHHIHHII',len(version),32,0xffff,16,0xffff,1,0,0x30,0x804,0,0)
Path('version.res').write_bytes(null_header+header+align(version))

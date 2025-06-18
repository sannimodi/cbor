#!/usr/bin/env python3
"""
Validation script to verify the CBOR → CbOr renaming was successful.
"""
import os
import re
from pathlib import Path

def main():
    base_dir = Path("/mnt/c/code/cbor")
    
    print("🔍 Validating CBOR → CbOr renaming...")
    print("=" * 50)
    
    # Check directory structure
    print("\n📁 Directory Structure:")
    expected_dirs = [
        "CbOrSample",
        "CbOrSerialization", 
        "CbOrSerialization.Demo",
        "CbOrSerialization.Generator",
        "CbOrSerialization.Tests"
    ]
    
    for dir_name in expected_dirs:
        dir_path = base_dir / dir_name
        status = "✅" if dir_path.exists() else "❌"
        print(f"  {status} {dir_name}")
    
    # Check key files
    print("\n📄 Key Files:")
    key_files = [
        "CbOrSerialization.sln",
        "CbOrSerializationSpec.md",
        "CbOrSample/CbOrSample.csproj",
        "CbOrSerialization/CbOrSerialization.csproj"
    ]
    
    for file_path in key_files:
        full_path = base_dir / file_path
        status = "✅" if full_path.exists() else "❌"
        print(f"  {status} {file_path}")
    
    # Check for old references in source files
    print("\n🔎 Checking for old 'CborSerialization' references:")
    old_refs = []
    
    for cs_file in base_dir.rglob("*.cs"):
        if "/obj/" in str(cs_file) or "/bin/" in str(cs_file):
            continue
            
        try:
            content = cs_file.read_text(encoding='utf-8')
            if "CborSerialization" in content and "System.Formats.Cbor" not in content:
                old_refs.append(str(cs_file))
        except:
            continue
    
    if old_refs:
        print("  ❌ Found old references in:")
        for ref in old_refs[:5]:  # Show first 5
            print(f"    - {ref}")
    else:
        print("  ✅ No old 'CborSerialization' references found")
    
    # Check for new references
    print("\n✨ Checking for new 'CbOrSerialization' references:")
    new_refs = 0
    
    for cs_file in base_dir.rglob("*.cs"):
        if "/obj/" in str(cs_file) or "/bin/" in str(cs_file):
            continue
            
        try:
            content = cs_file.read_text(encoding='utf-8')
            if "CbOrSerialization" in content:
                new_refs += 1
        except:
            continue
    
    print(f"  ✅ Found {new_refs} files with 'CbOrSerialization' references")
    
    # Check System.Formats.Cbor preservation
    print("\n🛡️  Checking System.Formats.Cbor preservation:")
    system_refs = 0
    
    for cs_file in base_dir.rglob("*.cs"):
        if "/obj/" in str(cs_file) or "/bin/" in str(cs_file):
            continue
            
        try:
            content = cs_file.read_text(encoding='utf-8')
            if "System.Formats.Cbor" in content:
                system_refs += 1
        except:
            continue
    
    print(f"  ✅ Found {system_refs} files with preserved 'System.Formats.Cbor' references")
    
    print("\n" + "=" * 50)
    print("🎉 Renaming validation complete!")

if __name__ == "__main__":
    main()
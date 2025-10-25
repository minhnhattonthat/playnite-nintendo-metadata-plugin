import os
import shutil
from glob import glob
from pathlib import Path

import yaml
from invoke import task

REPO = Path(os.path.dirname(__file__))


def get_version():
    manifest = (REPO / "extension.yaml").read_text()
    return yaml.safe_load(manifest)["Version"]


@task
def build(ctx):
    ctx.run("dotnet build -c Release")


@task
def test(ctx):
    ctx.run("dotnet test tests")

@task
def pack(ctx, toolbox="~/AppData/Local/Playnite/Toolbox.exe"):
    target = REPO / "dist/raw"
    if target.exists():
        shutil.rmtree(str(target))
    target.mkdir(parents=True)
    
    # Copy only necessary files, excluding system assemblies
    source = REPO / "bin/Release/"
    exclude_patterns = [
        "System.*.dll",
        "Microsoft.Win32.*.dll", 
        "netstandard.dll",
        "*.pdb"
    ]
    
    for item in source.iterdir():
        # Skip excluded patterns
        should_exclude = False
        for pattern in exclude_patterns:
            if item.match(pattern):
                should_exclude = True
                break
        
        if not should_exclude:
            if item.is_dir():
                shutil.copytree(item, target / item.name)
            else:
                shutil.copy2(item, target)

    toolbox = Path(toolbox).expanduser()
    ctx.run('"{}" pack "{}" dist'.format(toolbox, target))
    for file in glob(str(REPO / "dist/*.pext")):
        if "_" in file:
            shutil.move(file, str(REPO / "dist/Nintendo_Metadata_v{}.pext".format(get_version())))

    shutil.make_archive(str(REPO / "dist/Nintendo_Metadata_v{}".format(get_version())), "zip", str(target))


@task
def style(ctx):
    ctx.run("dotnet format src")

@task
def clean(ctx):
    shutil.rmtree("dist")

#!/usr/bin/python3
# coding=utf8

import os
import sys
import argparse
import subprocess

# Change directory to the solution root:
root = os.path.dirname(os.path.realpath(__file__))    
os.chdir(root)


parser = argparse.ArgumentParser(description='Executes XUnit test cases.')

parser.add_argument('-p', '--profile', dest='profile', action='store_true', help='Enables the mono profiler, collecting information about the resources used by the test cases')
parser.add_argument('-nc', '--no-color', dest='nocolor', action='store_true', help='Turns off colored output, which has been observed to cause problems in certain terminals.')

parser.add_argument(dest='assemblies', type=str, nargs='+',
                help='An assembly to search for XUnit test cases')

parser.add_argument('-c', '--class', dest='classes', type=str, default=[], nargs='*',
                help='Executes all the test cases of the class with the given, fully qualified name (including namespace!). If this is never given, *all* test cases of all given assemblies are executed.')

args = parser.parse_args()

monoArgs = ['mono']
if args.profile:
    monoArgs.append('--profile=log:report')
monoArgs.append('packages/xunit.runner.console.2.4.1/tools/net461/xunit.console.exe')
for a in args.assemblies:
    monoArgs.append('Binaries/Debug/{assembly}.dll'.format(assembly=a))
for cn in args.classes:
    monoArgs.append('-class')
    monoArgs.append(cn)
    
monoArgs.append('-verbose')

if args.nocolor:
    monoArgs.append('-nocolor')

sys.exit(subprocess.call(monoArgs, env={'LD_LIBRARY_PATH' : os.path.join(root, 'Binaries/Debug')}))

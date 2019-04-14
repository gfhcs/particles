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

parser.add_argument('-c', '--class', dest='classes', type=str, default=[], action='append',
                help='Limit execution to  the test cases of the class with the given, fully qualified name (including namespace!).')
parser.add_argument('-m', '--method', dest='methods', type=str, default=[], action='append',
                help='Limit execution to the specified test method. The name must be fully qualified (including namespace!), but may contain wildcards (*).')


args = parser.parse_args()

monoArgs = ['mono']
if args.profile:
    monoArgs.append('--profile=log:calls,output=profile.mlpd')
monoArgs.append('packages/xunit.runner.console.2.4.1/tools/net461/xunit.console.exe')
monoArgs.extend(args.assemblies)
for cn in args.classes:
    monoArgs.append('-class')
    monoArgs.append(cn)
for mn in args.methods:
    monoArgs.append('-method')
    monoArgs.append(mn)
    
monoArgs.append('-verbose')

if args.nocolor:
    monoArgs.append('-nocolor')

exitCode = subprocess.call(monoArgs, env={'LD_LIBRARY_PATH' : os.path.join(root, 'Binaries/Debug')})

if args.profile and exitCode == 0:
    exitCode=subprocess.call(['mprof-report', '--out=profile.txt', 'profile.mlpd'])
    subprocess.call(['rm', 'profile.mlpd'])

sys.exit(exitCode)

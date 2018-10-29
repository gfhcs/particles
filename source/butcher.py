#!/usr/bin/python3
# coding=utf8

# This script can be used to convert Butcher tableaus from Wikipedia into constructor calls for RungeKuttaIntegrator<Q, G>.

import sys
import itertools

class ParseError(Exception):
    pass

def obtain(msg, p):    
    while True:
        try:
            return p(input(msg))
        except ParseError as pe:
            print("Could not parse input: {msg} Try again:".format(msg=str(pe)))
        #except:
        #    print("Could not parse input! Try again:".format())

def parseFraction(f):
    try:
        parts = tuple(map(int, f.split("/")))
        if len(parts) == 1:
            return (parts[0], 1)
        elif len(parts) == 2:
            return parts
        else:
            raise ValueError()
    except:
        raise ParseError("Could not parse {f} as fraction!".format(f=f))
        
def parseMatrix(s):
    return list(map(parseFraction, s.split(",")))
    
def parseA(numStages, s):
    m = parseMatrix(s)
    
    if len(m) != numStages * numStages:
        raise ParseError("The matrix A must have shape {s}x{s}!".format(s=numStages))
    
    return [m[i : i + numStages] for i in range(0, len(m), numStages)]
            
def parseB(numStages, s):
    m = parseMatrix(s)
    
    if len(m) != numStages:
        raise ParseError("There must be exactly {s} coefficients b_j!".format(s=numStages))
    
    return m

def parseC(numStages, s):
    m = parseMatrix(s)
    
    if len(m) != numStages:
        raise ParseError("There must be exactly {s} coefficients c_j!".format(s=numStages))
    
    return m

def gcd(*numbers):
    a = None
    for x in numbers:
        
        if a is None:
            a = abs(x)
        
        b = abs(x)
        
        if a < b:
            a, b = x, a
            
        while b != 0:
            a, b = b, a % b
        
    if a is None:
        a = 1
    return a

def lcm(*numbers):
   
    lcm = None
    for n in numbers:
        
        if lcm is None:
            lcm = n
        
        product = lcm * n
        lcm = product // gcd(lcm, n)
        
    if lcm is None:
        lcm = 1
        
    return lcm

def d(fraction):
    return fraction[1]

def lowerLeft(matrix):
    i = 1
    for row in matrix:
        yield row[0:i]
        i = i + 1

if __name__ == '__main__' :
        
    s = obtain("Enter number s of stages:", int)
    
    A = obtain("Enter the matrix A (fractions, comma-separated, row after row, including zeros): ", lambda i : parseA(s, i))
    b = obtain("Enter coefficients b_j (fractions, comma-separated, including zeros): ", lambda i : parseB(s, i))
    c = obtain("Enter coefficients c_j (fractions, comma-separated, including zeros): ", lambda i : parseC(s, i))

    cd = lcm(*map(d, itertools.chain(b, c, *A)))
    
    prefix = 'Butcher array = '
    
    for n, d in itertools.chain(itertools.chain(*lowerLeft(A)), b, c):        
        print(prefix + str(n * (cd // d)), end='')
        prefix = ', '        
    print('')
    
    print ('Divisor: ' + str(cd))

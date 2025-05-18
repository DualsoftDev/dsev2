using System;
public class DynamicClass
{
    public static string Hello() => "Hello from Dynamic DLL!";
}

public class Calculator : Dual.Ev2.Interfaces.ICalculator
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}
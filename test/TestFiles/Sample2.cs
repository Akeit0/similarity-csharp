namespace TestFiles;

public class MathOperations
{
    // Similar to Add method in Calculator
    public int AddNumbers(int first, int second)
    {
        var sum = first + second;
        return sum;
    }
    
    // Different implementation
    public double Divide(double a, double b)
    {
        if (b == 0)
            throw new DivideByZeroException();
        return a / b;
    }
    
    // Similar to Multiply
    public int MultiplyNumbers(int num1, int num2)
    {
        return num1 * num2;
    }
}
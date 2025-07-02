namespace TestFiles;

public class Calculator
{
    public int Add(int a, int b)
    {
        var result = a + b;
        return result;
    }
    
    public int Sum(int x, int y)
    {
        var total = x + y;
        return total;
    }
    
    public int Multiply(int a, int b)
    {
        return a * b;
    }
    
    public int Product(int x, int y)
    {
        return x * y;
    }

    public double Div(int a, int b)
    {
        if (b == 0)
            throw new Exception();
        return a / b;
    }
}
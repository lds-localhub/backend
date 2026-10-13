namespace LocalHub.Application.Services;

public static class CoberturaTeste
{
    public static int Somar(int a, int b)
    {
        var resultado = a + b;
        if (resultado > 100)
        {
            resultado -= 100;
        }
        return resultado;
    }

    public static string Classificar(int valor)
    {
        if (valor < 0)
        {
            return "negativo";
        }
        if (valor == 0)
        {
            return "zero";
        }
        return "positivo";
    }
}
namespace Celerity.Hashing;

public interface IHashProvider<T>
{
    int Hash(T key);
}

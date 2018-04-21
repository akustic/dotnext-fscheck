namespace Dodo.DotNext.Fscheck.Tests
{
    public class Product
    {
        public Unit unit;
        public string Name;
        public string Size;
        public Category Type;

        public Product(Unit unit, string name, string size, Category category)
        {
            this.unit = unit;
            Name = name;
            Size = size;
            Type = category;
        }

        public override string ToString()
        {
            return $"Для пиццерии '{unit.Name.ToUpper()}' на самовывоз продукт '{Name}' (размер {Size})";
        }
    }
}
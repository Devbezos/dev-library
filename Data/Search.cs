namespace DevClient.Data
{
    public class Search
    {
        public Search(string keyword, string store, List<Product> products)
        {
            Keyword = keyword;
            Store = store;
            Products = products;
        }
        public string Keyword { get; set; }
        public string Store { get; set; }
        public List<Product> Products { get; set; }
    }
    public class Product
    {
        public string Name { get; }
        public string Price { get; }
        public string Url { get; set; }

        public Product(string name, string price, string url)
        {
            Name = name;
            Price = price;
            Url = $"[{name}]({url}) {price}\n";
        }
    }
}





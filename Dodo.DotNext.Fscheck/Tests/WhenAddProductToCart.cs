using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FsCheck;
using HtmlElements.Extensions;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using RestSharp;

namespace Dodo.DotNext.Fscheck.Tests
{
    public class WhenAddProductToCart
    {
        private const string Url = "https://dodopizza.ru";

        private List<City> _cities;

        // Для ДЕМО (Verbose/ QuickThrowOnFailure)
        private readonly Configuration _configuration = Configuration.VerboseThrowOnFailure;
        private IWebDriver _currentDriver;

        [SetUp]
        public void Start()
        {
            _configuration.MaxNbOfTest = 2;
            // Replay FAILED Tests
            //_configuration.Replay = FsCheck.Random.StdGen.NewStdGen(1134107939,296438564);
            SetCities();
        }


        [Test]
        public void ProductIsInCart()
        {
            /* Arrange */
            Prop.ForAll(SetProducts(), SetDrivers(), (unitProduct, driverName) =>
            {
                /* Start Act */
                GoToUnitSite(driverName, unitProduct);
                var cardName = GetProductCard(_currentDriver, unitProduct);

                AddToCart(cardName);
                SelectCarryoutStore(_currentDriver, unitProduct);
                GoToCart(_currentDriver);
                /* End Act */

                /* Assert */
                var productsInCart = _currentDriver.FindElements(By.CssSelector("div.cart__line:not([class*='gift'])"));
                return (productsInCart.Count == 1)
                    .Label($"Наименований продукта в корзине: {productsInCart.Count}")
                    .And(productsInCart[0].Text.Contains(unitProduct.Name)
                        .Label($"Реальное наименование продукта: {productsInCart[0].Text}"))
                    // Разбиение на классы генерируемых кейсов
                    .Classify(unitProduct.Type.Equals(Category.SOUVENIR), "Сувениры")
                    .Classify(unitProduct.Type.Equals(Category.SNACK), "Закуски")
                    .Classify(unitProduct.Type.Equals(Category.DESSERT), "Десерты");
            }).Check(_configuration);
        }

        [Test]
        public void ProductIsInCartForShrinking()
        {
            _configuration.MaxNbOfTest = 3;
            var products = GetDifferentProductsForUnit();
            Prop.ForAll(products
                , SetDrivers(), (unitProducts, driverName) =>
                {
                    GoToUnitSite(driverName, unitProducts[0]);
                    foreach (var unitProduct in unitProducts)
                    {
                        var cardName = GetProductCard(_currentDriver, unitProduct);

                        AddToCart(cardName);
                        try
                        {
                            SelectCarryoutStore(_currentDriver, unitProduct);
                        }
                        catch (Exception exp)
                        {
                            TestContext.WriteLine("Не нужно выбирать тип доставки");
                        }
                    }

                    GoToCart(_currentDriver);
                    var productsInCart =
                        _currentDriver.FindElements(By.CssSelector("div.cart__line:not([class*='gift'])"));
                    return (productsInCart.Count == unitProducts.Length)
                        .Label($"Наименований продукта в корзине: {productsInCart.Count}");
                }).Check(_configuration);
        }


        [TearDown]
        public void Close()
        {
            _currentDriver?.Quit();
        }

        
        
        // FsCheck примеры

        [Test]
        // для ДЕМО Shrinking
        public void SimpleExampleWithShrinking()
        {
            _configuration.MaxNbOfTest = 100;
            Prop.ForAll<int>(number =>
            {
                var newNumber = GetNumberMoreThan10(number);
                return (newNumber > 10).Label($"Реальное число: {newNumber}");
            }).Check(_configuration);
        }


        [Test]
        public void GenTwoExample()
        {
            _configuration.MaxNbOfTest = 10;
            Prop.ForAll(Gen.Two(Gen.Elements(GetProductsFromDB().ToArray())).ToArbitrary().Filter(twoProducts =>
                twoProducts.Item1.unit.Dep.Name ==
                twoProducts.Item2.unit.Dep.Name
                &&
                twoProducts.Item1.Name != twoProducts.Item2.Name
            ), two => true).Check(_configuration);
        }


        [Test]
        public void GenArrayExample()
        {
            _configuration.MaxNbOfTest = 10;
            Prop.ForAll(Gen.ListOf(3, Gen.Elements(GetProductsFromDB().ToArray())).ToArbitrary()
                    .Filter(three => three.Select(product => product.unit.Dep.Name)
                                         .Distinct().Count() == 1), three => true)
                .Check(_configuration);
        }


        [Test]
        public void GenArrayExampleForSingleCity()
        {
            _configuration.MaxNbOfTest = 10;
            Prop.ForAll(Gen.ArrayOf(Gen.Elements(GetProductsFromDB().ToArray())).ToArbitrary()
                    .Filter(array => array.All(product => product.unit.Dep.Name == "Клин")
                                     && array.Length > 0), _ => true)
                .Check(_configuration);
        }


        [Test]
        public void GenConstExample()
        {
            _configuration.MaxNbOfTest = 10;
            Prop.ForAll(Gen.Constant(GetProductsFromDB()[0]).ToArbitrary()
                    , product => true)
                .Check(_configuration);
        }


        [Test]
        public void GenConvertExample()
        {
            _configuration.MaxNbOfTest = 4;
            var browsers = Arb.From(Gen.Elements("chrome", "firefox"))
                .Convert<string, IWebDriver>(name =>
                {
                    switch (name)
                    {
                        case "chrome":
                            return new ChromeDriver();
                        case "firefox":
                            return new FirefoxDriver();
                        default:
                            throw new Exception("Нет у нас такого браузера");
                    }
                }, name => "");
            Prop.ForAll(browsers
                    , browser => browser.Quit())
                .Check(_configuration);
        }


        private Arbitrary<string> SetDrivers()
        {
            return Gen.Elements("CHROME", "FIREFOX").ToArbitrary();
        }

        private Arbitrary<Product> SetProducts()
        {
            var products = new List<Product>();
            products = GetProductsFromDB();

            return Gen.Elements(products.ToArray())
                .ToArbitrary(); //.Filter(product => product.Type == Category.SOUVENIR);
            ;
            // ДЛЯ ДЕМО (Оставить только ЗАКУСКИ)
            //.Filter(product => product.Type == Category.SNACK);
        }

        #region Вспомогательные методы

        // Невалидный метод для проверки Shrinking 
        private int GetNumberMoreThan10(int number)
        {
            if (number > 0)
            {
                number = number + 1;
            }
            else if (number < 0)
            {
                number = Math.Abs(number) + 9;
            }
            else
            {
                number = number + 10;
            }

            return number;
        }

        private void GoToUnitSite(string driverName, Product unitProduct)
        {
            _currentDriver?.Quit();
            var driver = GetDriver(driverName);
            _currentDriver = driver;
            _currentDriver.Manage().Window.Maximize();
            _currentDriver.Manage().Cookies.DeleteAllCookies();
            var urlNew = $"{Url}{unitProduct.unit.Dep.Url}/{unitProduct.unit.Translit}";
            _currentDriver.Url = urlNew;
        }


        private IWebDriver GetDriver(string driverName)
        {
            switch (driverName)
            {
                case "CHROME":
                {
                    var options = new ChromeOptions();
                    options.AddUserProfilePreference("pageLoadStrategy", "normal");
                    options.AddUserProfilePreference("disable-popup-blocking", "true");
                    options.AddUserProfilePreference("intl.accept_languages", "ru");
                    return new ChromeDriver(options);
                }
                case "FIREFOX":
                {
                    var options = new FirefoxOptions();
                    options.PageLoadStrategy = PageLoadStrategy.Normal;
                    return new FirefoxDriver(options);
                }
                default:
                    throw new Exception($"Нет такого браузера у нас в тестах {driverName}");
            }
        }

        private List<Product> GetProductsFromDB()
        {
            var products = new List<Product>();
            var city = _cities.First(_ => _.Name.Equals("Клин"));
            var depKlin = new Departament(city.Name, city.Url);
            var unitKlin = new Unit(depKlin, "Клин", "Balakireva624");
            products.Add(new Product(unitKlin, "Влажная салфетка", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Книга «И ботаники делают бизнес 1+2»", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Открытка «День рождения»", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Подарочный сертификат", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Коллекционный магнит «Додо Пиццы»", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Очки «С новым Додо»", "1", Category.SOUVENIR));
            products.Add(new Product(unitKlin, "Додстер", "1", Category.SNACK));
            products.Add(new Product(unitKlin, "Картофель из печи", "1", Category.SNACK));
            products.Add(new Product(unitKlin, "Картофель с брынзой", "1", Category.SNACK));

            var citySaratov = _cities.First(_ => _.Name.Equals("Саратов"));
            var depSaratov = new Departament(citySaratov.Name, citySaratov.Url);
            var unitSaratov = new Unit(depSaratov, "Солнечный", "solnechnyi");
            products.Add(new Product(unitSaratov, "Книга «И ботаники делают бизнес 2»", "1", Category.SOUVENIR));
            products.Add(new Product(unitSaratov, "Додстер", "1", Category.SNACK));
            products.Add(new Product(unitSaratov, "Картофель из печи", "1", Category.SNACK));
            products.Add(new Product(unitSaratov, "Картофель с брынзой", "1", Category.SNACK));
            products.Add(new Product(unitSaratov, "Картофель из печи", "1", Category.SNACK));
            products.Add(new Product(unitSaratov, "Картофель с брынзой", "1", Category.SNACK));

            var cityVolgograd = _cities.First(_ => _.Name.Equals("Волгоград"));
            var depVolgograd = new Departament(cityVolgograd.Name, cityVolgograd.Url);
            var unitVolgograd = new Unit(depVolgograd, "Волгоград Кр Окт", "lenina111g#");
            products.Add(new Product(unitVolgograd, "Сырники", "4", Category.DESSERT));
            products.Add(new Product(unitVolgograd, "Кукис ванильный", "1", Category.DESSERT));
            products.Add(new Product(unitVolgograd, "Додстер", "1", Category.SNACK));
            products.Add(new Product(unitVolgograd, "Картофель из печи", "1", Category.SNACK));
            products.Add(new Product(unitVolgograd, "Салат цезарь", "1", Category.SNACK));

            var cityAchinsk = _cities.First(_ => _.Name.Equals("Ачинск"));
            var depAchinsk = new Departament(cityAchinsk.Name, cityAchinsk.Url);
            var unitAchinsk = new Unit(depAchinsk, "Ачинск", "mikrorayon1");
            products.Add(new Product(unitAchinsk, "Сытные палочки с чоризо", "1", Category.SNACK));
            products.Add(new Product(unitAchinsk, "Додстер", "1", Category.SNACK));
            products.Add(new Product(unitAchinsk, "Картофельные оладьи", "8", Category.SNACK));
            products.Add(new Product(unitAchinsk, "Кукуруза", "2", Category.SNACK));
            products.Add(new Product(unitAchinsk, "Фонданы", "2", Category.DESSERT));
            products.Add(new Product(unitAchinsk, "Рулетики с корицей", "8", Category.DESSERT));
            return products;
        }

        private void SetCities()
        {
            var client = new RestClient(Url);
            var request = new RestRequest(Method.GET);
            var response = client.Execute(request).Content;
            var regex1 = new Regex("\"url\":\"(\\/\\w*\\d*)\",\"del");
            var regex2 = new Regex("\"name\":\"([а-яёА-Я- .,()]*\\d*)\",\"translit");
            List<string> urls = new List<string>();
            var matchesUrls = regex1.Matches(response);
            foreach (Match match in matchesUrls)
            {
                urls.Add(match.Groups[1].Value);
            }

            List<string> names = new List<string>();
            var matchesNames = regex2.Matches(response);
            foreach (Match match in matchesNames)
            {
                names.Add(match.Groups[1].Value);
            }

            _cities = new List<City>();
            for (int i = 0; i < names.Count; i++)
            {
                _cities.Add(new City(names[i], urls[i]));
            }
        }

        private IWebElement GetProductCard(IWebDriver browser, Product unitProduct)
        {
            browser.WaitUntil(_ => _.FindElements(By.XPath(
                                           $"//div[contains(@class,'product__name')][contains(text(),'{unitProduct.Name}')]"))
                                       .Count > 0);
            var names = browser.FindElements(By.XPath(
                $"//div[contains(@class,'product__name')][contains(text(),'{unitProduct.Name}')]"));
            IWebElement cardName;
            if (names.Count > 1)
            {
                cardName = names.First(_ => _.Text.Contains(unitProduct.Size));
            }
            else
            {
                cardName = names[0];
            }

            return cardName;
        }

        public Arbitrary<Product[]> GetDifferentProductsForUnit()
        {
            return Gen.ArrayOf(Gen.Elements(GetProductsFromDB().ToArray())).ToArbitrary()
                .Filter(array => array.Select(product => product.unit.Name).Distinct().Count() == 1
                                 && array.Length > 0 &&
                                 array.Length == array.Select(product => product.Name).Distinct().Count());
        }

        private void AddToCart(IWebElement productCard)
        {
            var addToCart = productCard.FindElement(By.XPath("./parent::div//button"));
            addToCart.WaitFor(_ => _.Enabled, TimeSpan.FromSeconds(40));
            addToCart.Click();
            Thread.Sleep(4000);
        }

        private void SelectCarryoutStore(IWebDriver browser, Product unitProduct)
        {
            browser.WaitFor(_ => _.FindElements(By.CssSelector("li[class*='tab__item']")).Count == 2,
                TimeSpan.FromSeconds(40));
            browser.FindElement(By.CssSelector("li:not([class*=active])[class*='tab__item']")).Click();
            var store = browser.FindElements(By.CssSelector("label[for*='pizzeria']"))
                .First(_ => _.Text.Contains(unitProduct.unit.Name));
            store.Click();
            var select = browser.FindElement(By.CssSelector("button[class*='delivery-or-carry']"));
            select.Click();
            browser.WaitFor(_ => _.FindElements(By.CssSelector("button[class*='delivery-or-carry']")).Count == 0);
        }

        private void GoToCart(IWebDriver browser)
        {
            browser.WaitUntil(_ => !_.FindElements(By.CssSelector(".popup__dialog")).Any());
            browser.WaitUntil(_ => _.FindElement((By.CssSelector("a[class*='floating-cart']"))).Enabled);
            var cart = browser.FindElement(By.CssSelector("a[class*='floating-cart']"));
            cart.Click();
            browser.WaitUntil(_ => _.FindElements(By.CssSelector("div.cart__list")).Any(), TimeSpan.FromSeconds(40));
        }

        #endregion
    }
}
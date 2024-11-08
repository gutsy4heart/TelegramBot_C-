using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
namespace TelegramWeatherBOT
{

    public class Program
    {
        private static readonly string BotToken = "7567935117:AAE6AfWZji-spMV9ClxzhtXDASLBObOtdNw";  // Токен Telegram бота
        private static readonly string WeatherApiKey = "a24f83c4ee64459f92a110127240811";  // Ключ API OpenWeatherMap

        private static readonly TelegramBotClient botClient = new TelegramBotClient(BotToken);
        private static readonly HttpClient httpClient = new HttpClient();

        // Переменная для хранения текущего состояния
        private static string currentState = "none";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Бот запущен...");

            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.ReadLine();
            cts.Cancel();
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.Message || update.Message?.Text == null) return;

            var message = update.Message;
            Console.WriteLine($"Получено сообщение от {message.From.FirstName}: {message.Text}");

            // Обработка команд "1" и "2"
            if (message.Text == "1")
            {
                currentState = "awaiting_city";
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введите название города для получения прогноза погоды.",
                    cancellationToken: cancellationToken
                );
            }
            else if (message.Text == "2")
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Еще увидимся!",
                    cancellationToken: cancellationToken
                );
                currentState = "exit";
            }
            else if (currentState == "awaiting_city") // Ожидание ввода города
            {
                var city = message.Text.Trim();
                if (string.IsNullOrEmpty(city))
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Пожалуйста, введите корректное название города.",
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    var weatherInfo = await GetWeatherAsync(city);
                    await botClient.SendTextMessageAsync(message.Chat.Id, weatherInfo, cancellationToken: cancellationToken);
                    currentState = "none"; // Сброс состояния после получения прогноза
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    $"Привет, {message.From.FirstName}! Выберите команду:\n1 - Ввести город для прогноза погоды\n2 - Выход",
                    cancellationToken: cancellationToken
                );
            }
        }
        static async Task<string> GetWeatherAsync(string city)
        {
            try
            {
                // URL для запроса данных о текущей погоде
                var url = $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={WeatherApiKey}&units=metric&lang=ru";

                // Выполнение запроса и проверка статуса ответа
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return $"Город '{city}' не найден. Пожалуйста, проверьте правильность написания.";
                    }
                    else
                    {
                        return "Произошла ошибка при запросе данных о погоде. Пожалуйста, попробуйте позже.";
                    }
                }

                // Обработка JSON ответа, если запрос успешен
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var temp = json["main"]["temp"].ToObject<float>();
                var description = json["weather"][0]["description"].ToString();
                var cityName = json["name"].ToString();

                // Формирование сообщения с текущей погодой
                return $"Погода в {cityName} на текущий момент: {description}, температура: {temp}°C.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return "Не удалось получить данные о погоде. Пожалуйста, проверьте название города или попробуйте позже.";
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Ошибка Telegram API: {apiRequestException.ErrorCode}\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

    }
}

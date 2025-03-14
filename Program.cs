using System;
using System.IO;
using System.Threading.Tasks;
using WTelegram;
using TL;
using CommandLine;
using System.Linq;

namespace TelegramConsoleClient
{
  // Коды возврата программы
  public enum ExitCode
  {
    Success = 0,
    AuthorizationRequired = 1,
    CodeRequired = 2,
    PasswordRequired = 3,
    MessageSendingFailed = 4,
    ConnectionFailed = 5,
    InvalidTarget = 6,
    UnknownError = 7
  }

  class Program
  {
    // Поля для хранения параметров командной строки
    private static int _apiId;
    private static string _apiHash;
    private static string _phoneNumber;
    private static string _code;
    private static string _password;
    private static string _sessionFilePath = "telegram_session.dat";
    private static string _target;
    private static string _message;
    private static Client _client;

    static async Task<int> Main(string[] args)
    {
      try
      {
        // Ручной парсинг аргументов командной строки
        if (!ParseCommandLineArgs(args))
        {
          PrintUsage();
          return (int)ExitCode.UnknownError;
        }

        return await RunClientAsync();
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Ошибка: {ex.Message}");
        PrintUsage();
        return (int)ExitCode.UnknownError;
      }
    }

    private static bool ParseCommandLineArgs(string[] args)
    {
      for (int i = 0; i < args.Length; i++)
      {
        if (i + 1 >= args.Length) break;  // Предотвращаем выход за пределы массива

        switch (args[i])
        {
          case "-a":
          case "--apiId":
            if (int.TryParse(args[i + 1], out int apiId))
              _apiId = apiId;
            else
              return false;
            i++;
            break;
          case "-h":
          case "--apiHash":
            _apiHash = args[i + 1];
            i++;
            break;
          case "-p":
          case "--phone":
            _phoneNumber = args[i + 1];
            i++;
            break;
          case "-c":
          case "--code":
            _code = args[i + 1];
            i++;
            break;
          case "-w":
          case "--password":
            _password = args[i + 1];
            i++;
            break;
          case "-s":
          case "--sessionFile":
            _sessionFilePath = args[i + 1];
            i++;
            break;
          case "-t":
          case "--target":
            _target = args[i + 1];
            i++;
            break;
          case "-m":
          case "--message":
            _message = args[i + 1];
            i++;
            break;
        }
      }

      // Проверяем обязательные параметры
      return _apiId != 0 && !string.IsNullOrEmpty(_apiHash) &&
             !string.IsNullOrEmpty(_phoneNumber) && !string.IsNullOrEmpty(_target) &&
             !string.IsNullOrEmpty(_message);
    }

    private static void PrintUsage()
    {
      Console.WriteLine("Использование:");
      Console.WriteLine("telegram-cli.exe -a YOUR_API_ID -h YOUR_API_HASH -p +YOUR_PHONE -t @username -m \"Тестовое сообщение\"");
      Console.WriteLine();
      Console.WriteLine("Обязательные параметры:");
      Console.WriteLine("  -a, --apiId         Telegram API ID");
      Console.WriteLine("  -h, --apiHash       Telegram API Hash");
      Console.WriteLine("  -p, --phone         Номер телефона в международном формате");
      Console.WriteLine("  -t, --target        Имя пользователя (начинается с @) или номер телефона");
      Console.WriteLine("  -m, --message       Текст сообщения для отправки");
      Console.WriteLine();
      Console.WriteLine("Опциональные параметры:");
      Console.WriteLine("  -c, --code          Код авторизации (если требуется)");
      Console.WriteLine("  -w, --password      Пароль двухфакторной аутентификации (если включен)");
      Console.WriteLine("  -s, --sessionFile   Путь к файлу сессии (по умолчанию \"telegram_session.dat\")");
    }

    private static async Task<int> RunClientAsync()
    {
      try
      {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Функция для предоставления конфигурации клиенту
        string Config(string what)
        {
          switch (what)
          {
            case "api_id": return _apiId.ToString();
            case "api_hash": return _apiHash;
            case "phone_number": return _phoneNumber;
            case "verification_code": return _code;
            case "password": return _password;
            case "session_pathname": return _sessionFilePath;
            default: return null;
          }
        }

        // Инициализация клиента с помощью конфигурационной функции
        _client = new Client(Config);

        // Подписка на обновления (опционально)
        _client.OnOwnUpdates += (updates) => {
          Console.WriteLine($"Получено обновление: {updates.GetType().Name}");
          return Task.CompletedTask;
        };

        // Проверка авторизации
        var user = await _client.LoginUserIfNeeded();

        if (user == null)
        {
          if (string.IsNullOrEmpty(_code))
          {
            Console.Error.WriteLine("Требуется код авторизации. Запустите приложение снова с параметром --code");
            return (int)ExitCode.CodeRequired;
          }
          if (string.IsNullOrEmpty(_password))
          {
            Console.Error.WriteLine("Требуется пароль двухфакторной аутентификации. Запустите приложение снова с параметром --password");
            return (int)ExitCode.PasswordRequired;
          }
          return (int)ExitCode.AuthorizationRequired;
        }

        // Отправка сообщения
        var result = await SendMessageAsync(_target, _message);
        return result;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Ошибка: {ex.Message}");
        if (ex.InnerException != null)
          Console.Error.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
        return (int)ExitCode.UnknownError;
      }
      finally
      {
        _client?.Dispose();
      }
    }

    private static async Task<int> SendMessageAsync(string target, string message)
    {
      try
      {
        // Получение информации о целевом пользователе/канале
        InputPeer peer = null;

        // Проверяем, является ли цель пользователем (по имени пользователя)
        if (target.StartsWith("@"))
        {
          var resolved = await _client.Contacts_ResolveUsername(target.Substring(1));
          if (resolved?.User != null)
          {
            // Используем конструктор InputPeerUser с правильными параметрами
            var user = resolved.User;
            peer = new InputPeerUser(user.id, user.access_hash);
          }
          else if (resolved?.Chat != null)
          {
            // Используем конструктор InputPeerChat с правильными параметрами
            var chat = resolved.Chat;
            // Получаем доступ к id чата через приведение к Chat
            if (chat is Chat basicChat)
            {
              peer = new InputPeerChat(basicChat.id);
            }
          }
          else if (resolved?.Channel != null)
          {
            // Используем конструктор InputPeerChannel с правильными параметрами
            var channel = resolved.Channel;
            peer = new InputPeerChannel(channel.id, channel.access_hash);
          }
        }
        // Проверяем, является ли цель пользователем (по номеру телефона)
        else if (target.StartsWith("+"))
        {
          var contacts = await _client.Contacts_GetContacts();
          // Получаем пользователей из контактов
          var users = contacts.users.Values.ToArray();
          var user = users.FirstOrDefault(u => u.phone == target);

          if (user != null)
          {
            peer = new InputPeerUser(user.id, user.access_hash);
          }
        }

        if (peer == null)
        {
          Console.Error.WriteLine($"Не удалось найти пользователя или канал с идентификатором {target}");
          return (int)ExitCode.InvalidTarget;
        }

        // Отправка сообщения
        var sentMessage = await _client.SendMessageAsync(peer, message);
        Console.WriteLine($"Сообщение успешно отправлено. ID: {sentMessage.id}");
        return (int)ExitCode.Success;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
        return (int)ExitCode.MessageSendingFailed;
      }
    }
  }
}
using System.Text;
using CanDoItAll.Modules.Workspace.ApiAccess;

if (args.Length != 0) {
    Console.Error.WriteLine("No arguments are accepted. Enter the password at the hidden prompt or through redirected standard input.");
    return 2;
}

try {
    var password = ReadPassword();
    Console.WriteLine(new ApiPasswordService().Hash(password));
    return 0;
} catch (ArgumentException) {
    Console.Error.WriteLine("A password of 12 to 256 characters is required.");
    return 2;
}

static string ReadPassword() {
    if (Console.IsInputRedirected) {
        var buffer = new StringBuilder();
        while (Console.Read() is var value && value >= 0 && value != '\n') {
            if (value != '\r') {
                buffer.Append((char)value);
            }
            if (buffer.Length > 256) {
                throw new ArgumentException("Password input is too long.");
            }
        }
        return buffer.ToString();
    }
    Console.Error.Write("Password (hidden): ");
    var password = new StringBuilder();
    while (true) {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) {
            Console.Error.WriteLine();
            return password.ToString();
        }
        if (key.Key == ConsoleKey.Backspace && password.Length > 0) {
            password.Length--;
        } else if (!char.IsControl(key.KeyChar)) {
            password.Append(key.KeyChar);
        }
        if (password.Length > 256) {
            throw new ArgumentException("Password input is too long.");
        }
    }
}

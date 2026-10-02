using Npgsql; // פותח את ארגז הכלים של פוסטגרס ומאפשר לנו להשתמש בפקודות החיבור והטבלאות בקובץ הזה
// פותח את תיקיית הסייבר וההצפנה של סי שארפ כדי שנוכל להשתמש במנוע ה-SHA256
using System.Security.Cryptography;
using System.Text;
using System.Text.Json; // הכלים של json
using System.Net.Http.Json; // כלי לשליחת נתוני JSON דרך האינטרנט ל Brevo
using Microsoft.AspNetCore.Cors.Infrastructure; // כלים של הcors

// מפעיל את מנוע השרת של סי שארפ ומייצר את המחסן הראשי לבילדר
var builder = WebApplication.CreateBuilder(args);

// שליפת פרטי החיבור של מסד הנתונים מתוך קובץ ההגדרות
var databaseAddress = builder.Configuration.GetConnectionString("DefaultConnection");

// שליפת הנתונים של Brevo מתוך קובץ ההגדרות
var brevoApiKey = builder.Configuration["Brevo:ApiKey"];
var brevoTemplateId = int.Parse(builder.Configuration["Brevo:TemplateId"] ?? "1");

// הוספת כלי ה-CORS לארגז הכלים של השרת
builder.Services.AddCors();

// קורא לפעולה בילד שמטרתה לנעול את כל המחסן ולייצר את השרת האמיתי
var app = builder.Build();

void SetupCors(CorsPolicyBuilder policy)
{
    policy.AllowAnyOrigin();
    policy.AllowAnyMethod();
    policy.AllowAnyHeader();
}
app.UseCors(SetupCors); // הפעלת אישור הכניסה לכל הפניות מהאתר



string GetStatus()
{
    // מייצר את אובייקט עם הצינור עם הפרטים של המסד נתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    
    // פותח את צינור התקשורת בלייב מול המסד נתונים
    connection.Open();
    return "שרת חתימה פלוס ומסד הנתונים מחוברים ועובדים בהצלחה!";
}

string HashPassword(string password)
{
    // המרת הסיסמה ממחרוזת טקסט למערך של בתים    
    byte[] inputBytes = Encoding.UTF8.GetBytes(password);

    // מחזיר מערך חדש של אפס ואחדים באמצעות ערבול של SHA-256
    byte[] hashBytes = SHA256.HashData(inputBytes);

    // תרגום המערך המעורבל למחרוזת תווים קריאה (אותיות ומספרים)
    return Convert.ToHexString(hashBytes);
}

// פונקציה לשליחת קוד אימות במייל באמצעות Brevo
async Task SendEmail(string email, string code)
{
    using var client = new HttpClient(); // יצירת כלי הדפדפן הפנימי
    client.DefaultRequestHeaders.Add("api-key", brevoApiKey); // מוסיף כותרת אבטחה של Brevo

    // יצירת אובייקט נמען והשמת האימייל
    EmailUser user = new EmailUser();
    user.email = email;

    // יצירת מערך בגודל 1 ושמירת המשתמש בתא הראשון
    EmailUser[] userList = new EmailUser[1];
    userList[0] = user;

    // יצירת אובייקט והשמת הקוד בן 6 הספרות
    EmailCode codeData = new EmailCode();
    codeData.code = code;

    // יצירת הודעת המייל הכוללת
    EmailMessage message = new EmailMessage();
    message.to = userList;
    message.templateId = brevoTemplateId;
    message.@params = codeData;

    // שליחת החבילה לאינטרנט
    var response = await client.PostAsJsonAsync("https://api.brevo.com/v3/smtp/email", message);
    Console.WriteLine("סטטוס שליחת המייל: " + response.StatusCode);
}

//  פונקציה שמקבלת את טופס נתוני הרישום מהאתר ומחזירה הודעת טקסט שההרשמה הצליחה
async Task<string> RegisterUser(UserRegister data)
{
    // שולח את הסיסמה הרגילה של המשתמש לפעולת ההצפנה ושומר את הסיסמא המאובטחת
    string hashPass = HashPassword(data.Password);

    string verifyCode = Random.Shared.Next(100000, 1000000).ToString();
    string hashedCode = HashPassword(verifyCode);
    DateTime expireTime = DateTime.UtcNow.AddMinutes(10);
    
    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress); 
    connection.Open();

    // בדיקה האם שם המשתמש או האימייל כבר קיימים במערכת
    string checkSql = "SELECT id, is_verified FROM users WHERE email = @email OR username = @user";
        using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("@email", data.Email);
        checkCmd.Parameters.AddWithValue("@user", data.Username);

        using var checkReader = checkCmd.ExecuteReader();
        if (checkReader.Read() == true)
        {
           bool isVerified = checkReader.GetBoolean(1);
           checkReader.Close(); // סוגרים את הקורא כדי שנוכל להמשיך להרשמה

        // אם המשתמש כבר אומת בעבר חוסמים הרשמה כפולה
        if (isVerified == true)
        {
            return "שם המשתמש או כתובת האימייל שהוזנו כבר קיימים במערכת, אנא התחבר לחשבונך.";
        }

        // אם הוא עדיין לא מאומת מעדכנים את הפרטים ומפיקים קוד חדש באותה שורה
        string updateSql = "UPDATE users SET full_name = @name, username = @user, password_hash = @pass, " +
       "reset_code_hash = @code, reset_code_expires = @expires WHERE email = @email";
        using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@name", data.FullName);
        updateCmd.Parameters.AddWithValue("@user", data.Username);
        updateCmd.Parameters.AddWithValue("@email", data.Email);
        updateCmd.Parameters.AddWithValue("@pass", hashPass);
        updateCmd.Parameters.AddWithValue("@code", hashedCode);
        updateCmd.Parameters.AddWithValue("@expires", expireTime);
        updateCmd.ExecuteNonQuery(); // מבצע את הפקודה למסד נתונים

        Console.WriteLine("קוד האימות החדש עבור " + data.Email + " הוא: " + verifyCode);
        await SendEmail(data.Email, verifyCode); // שליחת הקוד למייל דרך Brevo

        return "נרשמת בהצלחה, קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך לצורך הפעלת החשבון.";
    }
    checkReader.Close(); // סוגרים את הקורא כדי לפנות את צינור התקשורת להכנסת משתמש חדש

    // משפט ה-SQL להכנסת המשתמש והקוד אימות לטבלה
    string sql = "INSERT INTO users (full_name, username, email, password_hash, reset_code_hash, reset_code_expires) " +
    "VALUES (@name, @user, @email, @pass, @code, @expires)";
    // לוקח את הכינויים הזמניים (עם ה@) וממלא אותם בנתונים האמיתיים מהאתר
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@name", data.FullName);
    command.Parameters.AddWithValue("@user", data.Username);
    command.Parameters.AddWithValue("@email", data.Email);
    command.Parameters.AddWithValue("@pass", hashPass);
    command.Parameters.AddWithValue("@code", hashedCode);
    command.Parameters.AddWithValue("@expires", expireTime);

    // הפעלת הפקודה במסד הנתונים
    command.ExecuteNonQuery();

    Console.WriteLine("קוד האימות עבור " + data.Email + " הוא: " + verifyCode);
    await SendEmail(data.Email, verifyCode); // שליחת הקוד למייל דרך Brevo

    return "נרשמת בהצלחה, קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך לצורך הפעלת החשבון.";
}

// פונקציית התחברות עם אימות דו שלבי (2FA)
async Task<string> LoginUser(UserLogin data)
{
    // מצפין את הסיסמה שהמשתמש הקליד עכשיו כדי להשוות למה ששמור בטבלה
    string hashPass = HashPassword(data.Password);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // שולפים את הסיסמה, סטטוס האימות ואת האימייל האמיתי (גם אם התחבר עם שם משתמש)
    string sql = "SELECT password_hash, is_verified, email FROM users WHERE email = @loginInput OR username = @loginInput"; 
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@loginInput", data.Email);
    
    // מריצים את הפקודה ומקבלים את הנתונים 
    using var reader = command.ExecuteReader();

    // האם פוסטגרס מצא שורה בכלל
    if (reader.Read() == false)
    {
        return "המשתמש אינו קיים במערכת בדוק את פרטי ההתחברות ונסה שנית.";
    }

    // שולפים את הנתונים מהשורה
    string savedPassword = reader.GetString(0);
    bool isVerified = reader.GetBoolean(1); // שולף האם החשבון מאומת
    string userEmail = reader.GetString(2); // שולפים את כתובת האימייל האמיתית לשליחה

    // האם הסיסמה שהוקלדה עכשיו מתאימה למה ששמור
    if (hashPass == savedPassword)
    {
        // חסימה אם החשבון עוד לא אומת
        if (isVerified == false)
        {
             return "המשתמש אינו קיים במערכת בדוק את פרטי ההתחברות ונסה שנית.";
        }

        reader.Close(); // סוגרים את הקורא כדי שנוכל לעדכן את השורה בטבלה

        // הפקת קוד אימות חדש בן 6 ספרות והצפנתו
        string loginCode = Random.Shared.Next(100000, 1000000).ToString(); // הסבר במחברת לחפש על שם משתנה אחר
        string hashedCode = HashPassword(loginCode);
        DateTime expireTime = DateTime.UtcNow.AddMinutes(10); // תוקף ל10 דקות

        // שמירת קוד האימות במסד הנתונים
        string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire WHERE email = @email";
        using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@code", hashedCode);
        updateCmd.Parameters.AddWithValue("@expire", expireTime);
        updateCmd.Parameters.AddWithValue("@email", userEmail);
        updateCmd.ExecuteNonQuery(); // מבצע את הפעולה  

        // שליחת המייל ל-Brevo
        Console.WriteLine("קוד אימות להתחברות עבור " + userEmail + " הוא: " + loginCode);
        await SendEmail(userEmail, loginCode);

        return "שלחנו קוד אימות בן 6 ספרות לכתובת האימייל שלך לצורך השלמת תהליך ההתחברות.";   
    }
    else
    {
        return "שם המשתמש או הסיסמה שגויים, אנא בדוק את הפרטים ונסה שוב.";  
    }
}

async Task<string> CheckUserForReset(UserReset data)
{
    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // בדיקה האם יש משתמש שגם השם וגם האימייל תואמים לו
    string sql = "SELECT id FROM users WHERE username = @user AND email = @email";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@user", data.Username);
    command.Parameters.AddWithValue("@email", data.Email);

  // מריץ את המשתנה הסופי בטבלה ומקבל בחזרה צינור נתונים כדי לבדוק אם נמצא המשתמש
    using var reader = command.ExecuteReader();

    // אם לא נמצא משתמש כזה
    if (reader.Read() == false)
    {
        return "הפרטים שהוזנו אינם תואמים לפרטים במערכת, יש לבדוק את הפרטים ולנסות שוב";
    }
    reader.Close(); // סוגרים את הקורא למסד נתונים כדי שנוכל להריץ פקודת שמירה

    // הגרלת קוד 6 ספרות והצפנתו
    string secretCode = Random.Shared.Next(100000, 1000000).ToString();
    string hashedCode = HashPassword(secretCode); // מצפין את הקוד
    DateTime expireTime = DateTime.UtcNow.AddMinutes(10); // יוצר 10 דק לקוד

    //  שמירת הקוד המוצפן וזמן עד הוא פג במסד הנתונים
    string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire WHERE email = @email";
    using var updateCmd = new NpgsqlCommand(updateSql, connection); 
    updateCmd.Parameters.AddWithValue("@code", hashedCode);
    updateCmd.Parameters.AddWithValue("@expire", expireTime);
    updateCmd.Parameters.AddWithValue("@email", data.Email);
    updateCmd.ExecuteNonQuery();

    Console.WriteLine($"[אימות] קוד האימות עבור {data.Email} הוא: {secretCode}");
    await SendEmail(data.Email, secretCode); // שליחת הקוד לפונקצת שליחת מייל דרך Brevo

    return "קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך, אנא הזן אותו בכדי להמשך.";
}


string VerifyResetCode(VerifyCodeRequest data)
{
    //  מצפינים את הקוד שהמשתמש הקליד עכשיו כדי להשוות ל-Hash ששמור
    string hashedInputCode = HashPassword(data.Code);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // שליפת הקוד השמור וזמן התפוגה לפי האימייל או שם המשתמש
    string sql = "SELECT reset_code_hash, reset_code_expires FROM users WHERE email = @email OR username = @email";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@email", data.Email);

    using var reader = command.ExecuteReader();
    if (reader.Read() == false)
    {
        return "לא נמצא חשבון המקושר לכתובת אימייל זו. אנא בדוק את הפרטים.";
    }

    // בדיקה האם קוד האימות ריק בטבלה
    if (reader.IsDBNull(0) || reader.IsDBNull(1))
    {
        return "לא נמצא קוד אימות בתוקף עבור משתמש זה, יש לבקש קוד חדש.";
    }
    // שולפים את הנתונים מהעמודות
    string savedHashedCode = reader.GetString(0);
    DateTime expireTime = reader.GetDateTime(1);

    // בדיקה האם עברו 10 דקות (הקוד פג תוקף)
    if (DateTime.UtcNow > expireTime)
    {
     return "פג תוקף הקוד שהוזן למען ביטחונך, יש להפיק קוד חדש לאיפוס הסיסמה.";  
    }

    // בדיקה האם הקוד המוצפן שהוקלד תואם למה ששמור
    if (hashedInputCode != savedHashedCode)
    {
     return "קוד האימות שהוזן אינו תואם, אנא בדוק את הפרטים ונסה שנית.";    
    }

    reader.Close();
    // מחיקת קוד האימות מהטבלה והפיכת החשבון למאומת רשמית
    string clearCodeSql = "UPDATE users SET reset_code_hash = NULL, reset_code_expires = NULL, is_verified = true WHERE email = @email OR username = @email";
    using var clearCmd = new NpgsqlCommand(clearCodeSql, connection);
    clearCmd.Parameters.AddWithValue("@email", data.Email);
    clearCmd.ExecuteNonQuery();

    return "זהותך אומתה בהצלחה, הנך מועבר כעת לקביעת הסיסמה החדשה.";
}

// פונקציה לשליחה מחדש של קוד אימות
async Task<string> ResendCode(ResendCodeRequest data)
{
    // פתיחת החיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // בדיקה האם קיים משתמש עם האימייל הזה
    string checkSql = "SELECT id FROM users WHERE email = @email";
    using var checkCmd = new NpgsqlCommand(checkSql, connection); // צינור התחברות 
    checkCmd.Parameters.AddWithValue("@email", data.Email);

    using var reader = checkCmd.ExecuteReader();
    if (reader.Read() == false) // בודק אם נמצאה שורה בסד נתונים
    { 
        return "כתובת האימייל שהוזנה אינה רשומה במערכת, אנא בדוק את הפרטים.";  
    }
    reader.Close(); // סוגרים את הקורא כדי שנוכל לבצע עדכון

    // הפקת קוד חדש בן 6 ספרות והצפנתו
    // יש הסברים במחברת 
    string newCode = Random.Shared.Next(100000, 1000000).ToString();
    string hashedCode = HashPassword(newCode);
    DateTime expireTime = DateTime.UtcNow.AddMinutes(10); // תוקף ל-10 דקות

    // שמירת הקוד החדש במסד הנתונים
    string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire WHERE email = @email";
    using var updateCmd = new NpgsqlCommand(updateSql, connection);
    updateCmd.Parameters.AddWithValue("@code", hashedCode);
    updateCmd.Parameters.AddWithValue("@expire", expireTime);
    updateCmd.Parameters.AddWithValue("@email", data.Email);
    updateCmd.ExecuteNonQuery(); // מבצע את הפקודה

    // שליחת המייל ל-Brevo
    Console.WriteLine("קוד האימות שנשלח שוב עבור " + data.Email + " הוא: " + newCode);
    await SendEmail(data.Email, newCode); // קורא לפונקציה ששולחת מייל עם המייל והקוד

    return "קוד אימות חדש נשלח לכתובת האימייל שלך לתשומת ליבך, תוקף הקוד הינו ל-10 דקות בלבד.";
}

string UpdatePassword(UpdatePasswordRequest data)
{
    // מצפינים את הסיסמה החדשה ב-SHA-256
    string newHashedPass = HashPassword(data.NewPassword);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // עדכון הסיסמה ומחיקת קוד האיפוס (שלא ישתמשו בו שוב)
    string sql = "UPDATE users SET password_hash = @newPass, reset_code_hash = NULL, reset_code_expires = NULL WHERE email = @email";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@newPass", newHashedPass);
    command.Parameters.AddWithValue("@email", data.Email);

    int count = command.ExecuteNonQuery();
    if (count == 0)
    {
     return "שם המשתמש או הסיסמה אינם תקינים, אנא בדוק את הפרטים.";  
    }
    return "הסיסמה החדשה שונתה בהצלחה, כעת ניתן להתחבר לחשבונך.";
}

// התחברות עם גוגל
async Task<string> GoogleLogin(GoogleLoginRequest data)
{
    string email = "";
    string name = "";
    try
    {
        // פנייה לגוגל לאימות הטוקן ושליפת פרטי המשתמש
        // מקימים דפדפן פנימי זמני בשרת כדי לפתוח צינור תקשורת החוצה לאינטרנט
        using var httpClient = new HttpClient();
        var googleResponse = await httpClient.GetStringAsync("https://oauth2.googleapis.com/tokeninfo?id_token=" + data.Token);
        
        // קריאת האימייל והשם מתוך התשובה של גוגל
        using var jsonDoc = JsonDocument.Parse(googleResponse);
        email = jsonDoc.RootElement.GetProperty("email").GetString() ?? ""; // שולפים את המייל המלא של המשתמש 
        name = jsonDoc.RootElement.GetProperty("name").GetString() ?? ""; // שולפים את השם המלא של המשתמש
    }
    catch
    {
        return "אימות החשבון מול Google נכשל, אנא נסה שנית.";
    }

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // בדיקה האם המשתמש כבר קיים בטבלה
    string checkSql = "SELECT id FROM users WHERE email = @email";
    using var checkCmd = new NpgsqlCommand(checkSql, connection);
    checkCmd.Parameters.AddWithValue("@email", email);
    
    using var reader = checkCmd.ExecuteReader();
    if (reader.Read() == true)
    {
        // המשתמש קיים מחברים אותו ישר!
       return "ההתחברות באמצעות Google בוצעה בהצלחה, לחץ המשך על מנת לעבור לסביבת העבודה שלך.";
    }
    reader.Close();

    // משתמש חדש רושמים אותו אוטומטית לטבלה users
    string usernameFromEmail = email.Split('@')[0]; // גוזר שם משתמש מהאימייל
    string insertSql = "INSERT INTO users (full_name, username, email, password_hash, is_verified) VALUES (@name, @user, @email, 'GOOGLE_AUTH', true)";
    using var insertCmd = new NpgsqlCommand(insertSql, connection);
    insertCmd.Parameters.AddWithValue("@name", name);
    insertCmd.Parameters.AddWithValue("@user", usernameFromEmail);
    insertCmd.Parameters.AddWithValue("@email", email);
    insertCmd.ExecuteNonQuery();

    return "החשבון נוצר בהצלחה באמצעות Google, מיד תועבר לסביבת העבודה שלך.";
}

//  פותח את דף הבית הראשי של השרת ומפעיל את הפונקציה למעלה כדי להציג את המשפט
app.MapGet("/", GetStatus); 

app.MapPost("/api/register", RegisterUser); // מחבר את פונקציית ה-RegisterUser 

app.MapPost("/api/login", LoginUser); // מחבר את פונקציית ה-LoginUser 

app.MapPost("/api/check-reset", CheckUserForReset); // מחבר את פונקציית ה-CheckUserForReset 

app.MapPost("/api/verify-reset-code", VerifyResetCode); // מחבר את פונקציית ה-VerifyResetCode 

app.MapPost("/api/update-password", UpdatePassword); // מחבר את פונקציית ה-UpdatePassword

app.MapPost("/api/google-login", GoogleLogin); // מחבר את פונקציית ה-GoogleLogin

app.MapPost("/api/resend-code", ResendCode); // מחבר את פונקציית ה-ResendCode

app.Run(); // מפעיל את השרת כדי שיתחיל להקשיב בלייב לבקשות של האתר

// מחלקה לקליטת נתוני טופס ההרשמה מהאתר
class UserRegister
{
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

// מחלקה לקליטת נתוני טופס ההתחברות מהאתר
class UserLogin
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

// מחלקה לקליטת נתוני אימות משתמש באיפוס סיסמה
class UserReset
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
}

// מחלקה לקליטת קוד האימות בן 6 הספרות מהאתר
class VerifyCodeRequest
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
}

// מחלקה לקליטת נתוני עדכון סיסמה חדשה
class UpdatePasswordRequest
{
    public string Email { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

// מחלקה לקליטת טוקן ההתחברות מגוגל
class GoogleLoginRequest
{
    public string Token { get; set; } = "";
}


// מחלקה שמייצגת את הנמען למייל
class EmailUser
{
    public string email { get; set; } = "";
}

// מחלקה שמייצגת את המשתנה של הקוד בתבנית של Brevo
class EmailCode
{
    public string code { get; set; } = "";
}

// מחלקה שמייצגת את כל הודעת המייל שנשלחת ל-Brevo
class EmailMessage
{
    public EmailUser[] to { get; set; } = new EmailUser[0];
    public int templateId { get; set; }
    public EmailCode @params { get; set; } = new EmailCode();
}

// מחלקה לקליטת בקשת שליחת קוד חוזר מהאתר
class ResendCodeRequest
{
    public string Email { get; set; } = "";
}
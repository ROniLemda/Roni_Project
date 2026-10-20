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

// שליפת המפתח הסודי של reCAPTCHA
var recaptchaSecretKey = builder.Configuration["Recaptcha:SecretKey"] ?? "";

// הוספת כלי ה-CORS לארגז הכלים של השרת
builder.Services.AddCors();

// קורא לפעולה בילד שמטרתה לנעול את כל המחסן ולייצר את השרת האמיתי
var app = builder.Build();

void SetupCors(CorsPolicyBuilder policy)
{
    // מאפשר גישה רק מהכתובות המורשות של האתר שלנו
    policy.WithOrigins("http://127.0.0.1:5500", "http://localhost:5500");
    policy.AllowAnyMethod();
    policy.AllowAnyHeader();
}

app.UseCors(SetupCors); // הפעלת אישור הכניסה לכל הפניות מהאתר

// פונקציה לתפיסת שגיאות והוספת כותרות אבטחה לכל תשובה שיוצאת מהשרת
async Task CatchError(HttpContext http, RequestDelegate next)
{
    // כותרת אבטחה למניעת הצגת האתר בתוך מסגרת והגנה מפני התקפת קליק גקינג
    http.Response.Headers["X-Frame-Options"] = "DENY";

    // כותרת אבטחה שמכריחה את הדפדפן להיצמד לסוג הקובץ המקורי ולמנוע הרצת קוד זדוני
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // כותרת אבטחה למניעת דליפה של פרטים רגישים מהכתובת בעת מעבר לאתרים חיצוניים
    http.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    try
    {
        // מנסה להמשיך לפעולה הבאה כרגיל
        await next(http);
    }
    catch (Exception ex)
    {
        // הדפסת הודעת השגיאה בטרמינל כדי לאתר תקלות בשרת
        Console.WriteLine("שגיאת שרת פנימית " + ex.Message);

        // אם קרתה שגיאה במסד הנתונים מחזירים קוד 500 והודעה ברורה
        http.Response.StatusCode = 500;
        await http.Response.WriteAsync("אירעה שגיאה בתקשורת מול מסד הנתונים, אנא נסה שוב מאוחר יותר.");
    }
}

app.Use(CatchError); // חיבור פונקציית תפיסת השגיאות לשרת
app.UseWebSockets(); // הפעלת תמיכה בתקשורת סוקט בשרת


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

    // מחזיר מערך חדש של אפס ואחדים באמצעות הצפנה של SHA-256
    byte[] hashBytes = SHA256.HashData(inputBytes);

    // תרגום המערך המוצפן למחרוזת תווים קריאה של אותיות ומספרים
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
    // בדיקה שכל השדות הוזנו כראוי
    if (data.FullName == null || data.FullName == "" ||
        data.Username == null || data.Username == "" ||
        data.Password == null || data.Password == "")
    {
        return "נא למלא את כל שדות החובה.";
    }

    // בדיקת תקינות אימייל שלא ריק ומכיל שטרודל
    if (data.Email == null || data.Email == "" || data.Email.Contains('@') == false)
    {
        return "כתובת האימייל שהוזנה אינה תקינה.";
    }

    // בדיקת אורך מינימלי בשרת
    if (data.FullName.Length < 2 || data.Username.Length < 3 || data.Password.Length < 6)
    {
        return "אחד או יותר מהשדות קצרים מהמותר.";
    }

    // בדיקת אבטחה בשרת לוודא שאף שדה אינו חורג מהאורך המקסימלי המותר
    if (data.FullName.Length > 50 || data.Username.Length > 30 || data.Email.Length > 100 || data.Password.Length > 100)
    {
        return "אחד או יותר מהשדות שהוזנו ארוכים מהמותר, נא לעדכן את הפרטים ולנסות שוב.";
    }

    // שולח את הסיסמה הרגילה של המשתמש לפעולת ההצפנה ושומר את הסיסמא המאובטחת
    string hashPass = HashPassword(data.Password);

    // הגרלת קוד 6 ספרות באמצעות מחולל מאובטח שלא ניתן לחזות מראש
    // שודרג מ Random.Shared.Next לשמירה על תקן אבטחה גבוה
    string verifyCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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

        // אם הוא עדיין לא מאומת מעדכנים את הפרטים, מפיקים קוד חדש ומאפסים את מונה הניסיונות
        string updateSql = "UPDATE users SET full_name = @name, username = @user, password_hash = @pass, " +
        "reset_code_hash = @code, reset_code_expires = @expires, otp_count = 0 WHERE email = @email";
        using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@name", data.FullName);
        updateCmd.Parameters.AddWithValue("@user", data.Username);
        updateCmd.Parameters.AddWithValue("@email", data.Email);
        updateCmd.Parameters.AddWithValue("@pass", hashPass);
        updateCmd.Parameters.AddWithValue("@code", hashedCode);
        updateCmd.Parameters.AddWithValue("@expires", expireTime);
        updateCmd.ExecuteNonQuery(); // מבצע את הפקודה למסד נתונים

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

    await SendEmail(data.Email, verifyCode); // שליחת הקוד למייל דרך Brevo

    return "נרשמת בהצלחה, קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך לצורך הפעלת החשבון.";
}

// פונקציית התחברות עם אימות דו שלבי (2FA)
async Task<string> LoginUser(UserLogin data)
{
    //  פנייה לגוגל לוודא שהאימות אכן אמיתי ולא בוט
    using var httpClient = new HttpClient();
    string verifyUrl = "https://www.google.com/recaptcha/api/siteverify?secret=" + recaptchaSecretKey + "&response=" + data.CaptchaToken;
    string captchaResponse = await httpClient.GetStringAsync(verifyUrl);

    // קריאת התשובה של גוגל
    using var jsonDoc = JsonDocument.Parse(captchaResponse);
    bool isHuman = jsonDoc.RootElement.GetProperty("success").GetBoolean();

    if (isHuman == false)
    {
     return "אימות האבטחה של reCAPTCHA נכשל, אנא אשר שאינך רובוט ונסה שנית.";
    }
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

        // הפקת קוד אימות חדש בן 6 ספרות באמצעות מחולל מאובטח והצפנתו
        // שודרג מ Random.Shared.Next להגנה מירבית בתהליך ההתחברות הסבר במחברת
        string loginCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        string hashedCode = HashPassword(loginCode);
        DateTime expireTime = DateTime.UtcNow.AddMinutes(10); // תוקף ל10 דקות

        // שמירת קוד האימות במסד הנתונים ואיפוס מונה הטעויות
        string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire, otp_count = 0 WHERE email = @email";
        using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@code", hashedCode);
        updateCmd.Parameters.AddWithValue("@expire", expireTime);
        updateCmd.Parameters.AddWithValue("@email", userEmail);
        updateCmd.ExecuteNonQuery(); // מבצע את הפעולה  

        // שליחת המייל ל-Brevo
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

    // הגרלת קוד 6 ספרות באמצעות מחולל מאובטח והצפנתו
    // שודרג מ Random.Shared.Next להגנה מירבית בתהליך איפוס סיסמה
    string secretCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    string hashedCode = HashPassword(secretCode); // מצפין את הקוד
    DateTime expireTime = DateTime.UtcNow.AddMinutes(10); // יוצר 10 דק לקוד

    //  שמירת הקוד המוצפן וזמן פקיעתו במסד הנתונים ואיפוס מונה הטעויות
    string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire, otp_count = 0 WHERE email = @email";
    using var updateCmd = new NpgsqlCommand(updateSql, connection); 
    updateCmd.Parameters.AddWithValue("@code", hashedCode);
    updateCmd.Parameters.AddWithValue("@expire", expireTime);
    updateCmd.Parameters.AddWithValue("@email", data.Email);
    updateCmd.ExecuteNonQuery();

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

    // שליפת הקוד השמור וזמן התפוגה וכמות הטעויות לפי האימייל או שם המשתמש
    string sql = "SELECT reset_code_hash, reset_code_expires, full_name, email, otp_count FROM users WHERE email = @email OR username = @email";
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
    string fullName = reader.GetString(2);
    string verifiedEmail = reader.GetString(3);

    // שליפת כמות הניסיונות השגויים שבוצעו עד עכשיו מעמודת otp_count
    int attempts = 0;
    if (reader.IsDBNull(4) == false)
    {
        attempts = reader.GetInt32(4);
    }

    // בדיקה האם עברו 10 דקות (הקוד פג תוקף)
    if (DateTime.UtcNow > expireTime)
    {
        return "פג תוקף הקוד שהוזן למען ביטחונך, יש להפיק קוד חדש לאיפוס הסיסמה.";  
    }

    // בדיקה האם הקוד המוצפן שהוקלד תואם למה ששמור
    if (hashedInputCode != savedHashedCode)
    {
        reader.Close(); // סוגרים את הקורא כדי שנוכל לבצע פעולת עדכון בטבלה
        int newAttempts = attempts + 1; // העלאת משתנה הטעויות ב1

        // אם המשתמש הגיע ל5 טעויות מוחקים את הקוד כדי למנוע ניחושים
        if (newAttempts >= 5)
        {
            // מחיקת קוד האימות ואיפוס המשתנה במסד למניעת שימוש חוזר בקוד שנחסם
            string lockSql = "UPDATE users SET reset_code_hash = NULL, reset_code_expires = NULL, otp_count = 0 WHERE email = @email OR username = @email";
            using var lockCmd = new NpgsqlCommand(lockSql, connection);
            lockCmd.Parameters.AddWithValue("@email", data.Email);
            lockCmd.ExecuteNonQuery();

            return "הזנת קוד שגוי 5 פעמים, למען ביטחונך קוד האימות בוטל ויש להפיק קוד חדש.";
        }
        else
        {
            // שמירת מספר הניסיונות השגויים המעודכן במסד הנתונים
            string updateSql = "UPDATE users SET otp_count = @count WHERE email = @email OR username = @email";
            using var updateCmd = new NpgsqlCommand(updateSql, connection);
            updateCmd.Parameters.AddWithValue("@count", newAttempts);
            updateCmd.Parameters.AddWithValue("@email", data.Email);
            updateCmd.ExecuteNonQuery();

            // חישוב כמה ניסיונות נותרו למשתמש לפני שהקוד יימחק
            int remaining = 5 - newAttempts;
            return "קוד האימות שהוזן אינו תואם, נותרו לך עוד " + remaining + " ניסיונות לפני חסימת הקוד.";
        }
    }

    reader.Close(); // סוגרים את הקורא כי הקוד נמצא נכון ותקין

    // יצירת טוקן התחברות ייחודי של 32 תווים ללא מקפים שמשמש כרטיס כניסה למערכת
    string authToken = Guid.NewGuid().ToString("N");

    // הצפנת הטוקן באמצעות SHA-256 כך שבמסד הנתונים יישמר רק קוד מוצפן למניעת גניבת גישה במקרה של פריצה
    string hashedToken = HashPassword(authToken);

    // קביעת תוקף לטוקן למשך 24 שעות לפי שעון עולמי אחיד למניעת בעיות אזורי זמן
    DateTime tokenExpires = DateTime.UtcNow.AddDays(1);

    // מחיקת קוד האימות איפוס מונה הטעויות ל0 אימות החשבון ושמירת הטוקן המוצפן
    string clearCodeSql = "UPDATE users SET reset_code_hash = NULL, reset_code_expires = NULL, otp_count = 0, is_verified = true, auth_token = @token, token_expires = @expires WHERE email = @email OR username = @email";
    using var clearCmd = new NpgsqlCommand(clearCodeSql, connection);
    clearCmd.Parameters.AddWithValue("@token", hashedToken);
    clearCmd.Parameters.AddWithValue("@expires", tokenExpires);
    clearCmd.Parameters.AddWithValue("@email", data.Email);
    clearCmd.ExecuteNonQuery();

    return "זהותך אומתה בהצלחה, הנך מועבר כעת לקביעת הסיסמה החדשה.|" + fullName + "|" + verifiedEmail + "|" + authToken;
}

// פונקציה לשליחה מחדש של קוד אימות עם מנגנון הגנה מפני הצפת אימיילים
async Task<string> ResendCode(ResendCodeRequest data)
{
    // פתיחת צינור התקשורת מול מסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // שליפת מזהה המשתמש ותאריך התפוגה של הקוד הקיים לצורך בדיקת קצב הבקשות
    string checkSql = "SELECT id, reset_code_expires FROM users WHERE email = @email";
    using var checkCmd = new NpgsqlCommand(checkSql, connection); // צינור פקודה מאובטח למניעת הזרקות קוד
    checkCmd.Parameters.AddWithValue("@email", data.Email);

    using var reader = checkCmd.ExecuteReader(); // הרצת הבדיקה מול מסד הנתונים
    if (reader.Read() == false)
    { 
        // אם לא נמצא משתמש כזה מחזירים הודעה מתאימה
        return "כתובת האימייל שהוזנה אינה רשומה במערכת, אנא בדוק את הפרטים.";  
    }

    // מנגנון הגנה מתקדם בשרת מפני הצפה ושליחה מרובה של אימיילים
    // בדיקה האם קיים כבר תאריך תפוגה לקוד קודם במסד הנתונים
    if (reader.IsDBNull(1) == false)
    {
        // שליפת זמן התפוגה הקיים של הקוד הנוכחי
        DateTime lastExpires = reader.GetDateTime(1);

        // הקוד המקורי נוצר תמיד עם תוקף ל 10 דקות קדימה
        // לכן אם נותרו יותר מ 9 דקות עד לפקיעת התוקף סימן שעברה פחות מדקה מאז הבקשה הקודמת
        // במצב כזה אנחנו חוסמים את השליחה כדי למנוע ספאם ולהגן על מכסת השליחה ב Brevo
        if (lastExpires > DateTime.UtcNow.AddMinutes(9))
        {
            reader.Close(); // סגירת הקורא לפני היציאה מהפונקציה
            return "ניתן לבקש קוד חדש רק פעם אחת בדקה, אנא המתן מעט ונסה שנית.";
        }
    }

    reader.Close(); // סוגרים את הקורא כדי לפנות את החיבור להרצת פקודת העדכון הבאה

    // הגרלת קוד חדש בן 6 ספרות באמצעות מחולל אקראי קריפטוגרפי שלא ניתן לניחוש
    string newCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    // הצפנת הקוד בשיטת SHA256 כך שבמסד הנתונים יישמר רק ערך מוצפן
    string hashedCode = HashPassword(newCode);

    // קביעת תוקף חדש ל 10 דקות מרגע זה לפי שעון עולמי
    DateTime expireTime = DateTime.UtcNow.AddMinutes(10);

    // שמירת הקוד המוצפן החדש תאריך התפוגה ואיפוס מונה הטעויות ל 0
    string updateSql = "UPDATE users SET reset_code_hash = @code, reset_code_expires = @expire, otp_count = 0 WHERE email = @email";
    using var updateCmd = new NpgsqlCommand(updateSql, connection);
    updateCmd.Parameters.AddWithValue("@code", hashedCode);
    updateCmd.Parameters.AddWithValue("@expire", expireTime);
    updateCmd.Parameters.AddWithValue("@email", data.Email);
    updateCmd.ExecuteNonQuery(); // ביצוע פעולת העדכון בפועל בטבלת המשתמשים

    // שליחת הודעת הדואל עם הקוד הגלוי דרך שרת Brevo
    await SendEmail(data.Email, newCode);

    // החזרת הודעת אישור למשתמש
    return "קוד אימות חדש נשלח לכתובת האימייל שלך לתשומת ליבך, תוקף הקוד הינו ל-10 דקות בלבד.";
}

string UpdatePassword(UpdatePasswordRequest data)
{
    // מצפינים את הסיסמה החדשה ב-SHA-256
    string newHashedPass = HashPassword(data.NewPassword);

    // הצפנת הטוקן שהתקבל לצורך השוואה מול הטוקן המוצפן במסד הנתונים
    string hashedToken = HashPassword(data.Token);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // עדכון הסיסמה ומחיקת קוד האיפוס (שלא ישתמשו בו שוב)
    string sql = "UPDATE users SET password_hash = @newPass, auth_token = NULL, token_expires = NULL WHERE email = @email AND auth_token = @token AND token_expires > NOW()";   
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@newPass", newHashedPass);
    command.Parameters.AddWithValue("@email", data.Email);
    command.Parameters.AddWithValue("@token", hashedToken);

    int count = command.ExecuteNonQuery();
    if (count == 0)
    {
     return "אינך מורשה לבצע פעולה זו או שתוקף הפעולה פג, יש לבקש קוד לאימות מחדש."; 
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

        // בדיקת אבטחה: מוודאים שהטוקן הופק ספציפית עבור האתר שלנו ולא עבור אפליקציה זרה
        string audience = jsonDoc.RootElement.GetProperty("aud").GetString() ?? "";
        if (audience != "541544347287-kike3l6ru9nuroq4thre6tuvreffa8s2.apps.googleusercontent.com")
        {
            return "אימות החשבון מול Google נכשל, מזהה אפליקציה לא מורשה.";
        }

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

    // יצירת טוקן התחברות ייחודי של 32 תווים ללא מקפים עבור המשתמש שהתחבר דרך גוגל
    string authToken = Guid.NewGuid().ToString("N");

    // הצפנת הטוקן באמצעות SHA-256 לשמירה של ערך מוצפן בלבד במסד הנתונים
    string hashedToken = HashPassword(authToken);

    // קביעת תוקף לטוקן למשך 24 שעות לפי שעון עולמי
    DateTime tokenExpires = DateTime.UtcNow.AddDays(1);

    // בדיקה האם המשתמש כבר קיים בטבלה
    string checkSql = "SELECT id FROM users WHERE email = @email";
    using var checkCmd = new NpgsqlCommand(checkSql, connection);
    checkCmd.Parameters.AddWithValue("@email", email);
    
    using var reader = checkCmd.ExecuteReader();
    if (reader.Read() == true)
    {
        reader.Close();
        
        // עדכון הטוקן המוצפן בטבלה למשתמש קיים
        string updateSql = "UPDATE users SET auth_token = @token, token_expires = @expires WHERE email = @email";
        using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@token", hashedToken);
        updateCmd.Parameters.AddWithValue("@expires", tokenExpires);
        updateCmd.Parameters.AddWithValue("@email", email);
        updateCmd.ExecuteNonQuery();

        return "ההתחברות באמצעות Google בוצעה בהצלחה, לחץ המשך על מנת לעבור לסביבת העבודה שלך.|" + name + "|" + email + "|" + authToken;
    }

    // משתמש חדש רושמים אותו אוטומטית לטבלה users
    string usernameFromEmail = email.Split('@')[0]; // גוזר שם משתמש ראשוני מהאימייל לפני השטרודל

    // שמירת שם המשתמש במשתנה שנעדכן בכל סיבוב עד שיהיה תקין
    string finalUsername = usernameFromEmail;

    // משתנה דגל פשוט שבודק האם השם תקין ופנוי לשימוש
    bool isOk = false;

    // כל עוד השם עדיין לא בסדר ממשיכים לבדוק
    while (isOk == false)
    {
        // שאילתה לבדיקה האם שם המשתמש הנוכחי קיים בטבלה
        string checkUserSql = "SELECT id FROM users WHERE username = @user";
        using var checkUserCmd = new NpgsqlCommand(checkUserSql, connection);
        checkUserCmd.Parameters.AddWithValue("@user", finalUsername);

        // מריצים את השאילתה מול מסד הנתונים
        using var userReader = checkUserCmd.ExecuteReader();

        // אם השם תפוס במסד הנתונים
        if (userReader.Read() == true)
        {
            // סגירת הקורא כדי לאפשר הרצה נוספת
            userReader.Close();

            // הגרלת מספר אקראי של 4 ספרות בין 1000 ל 10000
            int randomSuffix = RandomNumberGenerator.GetInt32(1000, 10000);

            // חיבור המספר לשם המשתמש
            finalUsername = usernameFromEmail + "_" + randomSuffix;
        }
        else
        {
            // סגירת הקורא
            userReader.Close();

            // השם פנוי והכל בסדר ולכן משנים לאמת כדי לעצור את הלולאה
            isOk = true;
        }
    }

    // יצירת שאילתת הוספה עבור המשתמש החדש עם שם המשתמש הייחודי
    string insertSql = "INSERT INTO users (full_name, username, email, password_hash, is_verified, auth_token, token_expires) VALUES (@name, @user, @email, 'GOOGLE_AUTH', true, @token, @expires)";
    using var insertCmd = new NpgsqlCommand(insertSql, connection);
    insertCmd.Parameters.AddWithValue("@name", name);
    insertCmd.Parameters.AddWithValue("@user", finalUsername);
    insertCmd.Parameters.AddWithValue("@email", email);
    insertCmd.Parameters.AddWithValue("@token", hashedToken);
    insertCmd.Parameters.AddWithValue("@expires", tokenExpires);
    insertCmd.ExecuteNonQuery();

    return "החשבון נוצר בהצלחה באמצעות Google, מיד תועבר לסביבת העבודה שלך.|" + name + "|" + email + "|" + authToken;
}

// בדיקת אבטחה האם ה auth_token קיים בטבלה ובתוקף
string VerifyToken(TokenCheck data)
{
    // הצפנת הטוקן שהתקבל מהדפדפן לפני השוואתו מול הטוקן המוצפן במסד הנתונים
    string hashedToken = HashPassword(data.Token);

    // פתיחה החיבור למסד נתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // בודק אם יש משתמש עם הטוקן הזה שהתוקף שלו לא עבר
    string sql = "SELECT full_name, email FROM users WHERE auth_token = @token AND token_expires > NOW()";
    using var cmd = new NpgsqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@token", hashedToken);

    using var reader = cmd.ExecuteReader(); // מריץ אץ הפקודה במסד 
    if (reader.Read() == false)
    {
        return "הטוקן אינו תקין או פג תוקף";
    }

    string name = reader.GetString(0); 
    string email = reader.GetString(1);

    return "תקין|" + name + "|" + email;
}

// פונקציית התנתקות מחיקת הטוקן ממסד הנתונים כדי שלא ניתן יהיה להשתמש בו שוב
string LogoutUser(TokenCheck data)
{
    // הצפנת הטוקן לצורך איתור השורה של המשתמש במסד הנתונים
    string hashedToken = HashPassword(data.Token);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // איפוס הטוקן והתוקף לשורה של המשתמש בעל הטוקן הזה
    string sql = "UPDATE users SET auth_token = NULL, token_expires = NULL WHERE auth_token = @token";
    using var cmd = new NpgsqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@token", hashedToken);
    cmd.ExecuteNonQuery();

    return "ההתנתקות מהמערכת בוצעה בהצלחה, נשמח לראותך שוב בקרוב";
}

// פונקציה לשליפת כל המשתמשים עבור דף הניהול
// שימוש ב IResult מאפשר להחזיר שגיאת אבטחה 401 או הצלחה 200 עם הרשימה
IResult GetAdminUsers(string token = "")
{
    // בדיקת אבטחה אם הטוקן אינו שייך למנהל חוסמים מיד את הבקשה
    if (CheckIsAdmin(token) == false)
    {
        // החזרת קוד שגיאה 401 Unauthorized שאומר לדפדפן שאין הרשאה מתאימה
        return Results.Unauthorized();
    }

    // יצירת רשימה שתחזיק את כל המשתמשים רק לאחר שהאימות עבר בהצלחה
    List<AdminUser> usersList = new List<AdminUser>();

    // פתיחת צינור התקשורת למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // משפט ה-SQL לשליפת כל העמודות מסודרות לפי ה-ID מהקטן לגדול
    string sql = "SELECT id, full_name, username, email, password_hash, is_verified, reset_code_hash, reset_code_expires, auth_token, token_expires FROM users ORDER BY id ASC";
    
    using var command = new NpgsqlCommand(sql, connection);
    using var reader = command.ExecuteReader();

    // לולאה שעוברת שורה אחרי שורה במסד הנתונים
    while (reader.Read())
    {
        // יצירת אובייקט משתמש חדש עבור השורה הנוכחית
        AdminUser user = new AdminUser();

        // מזהה משתמש
        user.id = reader.GetInt32(0);

        // שם מלא
        if (reader.IsDBNull(1) == false)
        {
            user.full_name = reader.GetString(1);
        }

        // שם משתמש
        if (reader.IsDBNull(2) == false)
        {
            user.username = reader.GetString(2);
        }

        // אימייל
        if (reader.IsDBNull(3) == false)
        {
            user.email = reader.GetString(3);
        }

        // סיסמה מוצפנת
        if (reader.IsDBNull(4) == false)
        {
            user.password_hash = reader.GetString(4);
        }

        // האם מאומת
        if (reader.IsDBNull(5) == false)
        {
            user.is_verified = reader.GetBoolean(5);
        }

        // קוד אימות מוצפן (אם ריק נשאר ריק)
        if (reader.IsDBNull(6) == false)
        {
            user.reset_code_hash = reader.GetString(6);
        }

        // תוקף קוד אימות (תאריך)
        if (reader.IsDBNull(7) == false)
        {
            user.reset_code_expires = reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm:ss");
        }

        // טוקן התחברות פעיל
        if (reader.IsDBNull(8) == false)
        {
            user.auth_token = reader.GetString(8);
        }

        // תוקף טוקן התחברות
        if (reader.IsDBNull(9) == false)
        {
            user.token_expires = reader.GetDateTime(9).ToString("yyyy-MM-dd HH:mm:ss");
        }

        // הוספת המשתמש לרשימה
        usersList.Add(user);
    }

    // החזרת הרשימה המלאה עטופה בקוד סטטוס 200 OK והמרה אוטומטית לפורמט גייסון
    return Results.Ok(usersList);
}

// פונקציה למחיקת משתמש ממסד הנתונים לפי ID
string DeleteAdminUser(DeleteUserRequest data)
{
    // בדיקת אבטחה אם הטוקן אינו שייך למנהל חוסמים את הפעולה
    if (CheckIsAdmin(data.Token) == false)
    {
        return "גישה נדחתה, אין לך את ההרשאות המתאימות לביצוע הפעולה המבוקשת.";
    }

    // פתיחת צינור התקשורת למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // בדיקת הגנה למניעת מחיקה עצמית של מנהל המערכת המחובר
    // שאילתה לשליפת מזהה המשתמש של המנהל לפי הטוקן המוצפן שלו
    string adminIdSql = "SELECT id FROM users WHERE auth_token = @token";
    using var checkSelfCmd = new NpgsqlCommand(adminIdSql, connection);
    checkSelfCmd.Parameters.AddWithValue("@token", HashPassword(data.Token));
    using var adminReader = checkSelfCmd.ExecuteReader();
    if (adminReader.Read() == true)
    {
        // שליפת המזהה של המנהל הנוכחי מתוך השורה שנמצאה
        int currentAdminId = adminReader.GetInt32(0);
        adminReader.Close(); // סגירת הקורא כדי לפנות את צינור התקשורת

        // אם המנהל מנסה למחוק את החשבון האישי שלו חוסמים את הפעולה מיד
        if (currentAdminId == data.Id)
        {
            return "פעולה זו נחסמה, מנהל מערכת אינו רשאי למחוק את חשבונו האישי.";
        }
    }
    else
    {
        adminReader.Close(); // סגירת הקורא גם במקרה שלא נמצאה שורה
    }

    // משפט SQL למחיקת השורה שה id שלה תואם
    string sql = "DELETE FROM users WHERE id = @id";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@id", data.Id);

    // ביצוע הפקודה במסד הנתונים
    command.ExecuteNonQuery();

    // שמירת פעולת המחיקה ביומן הפעולות של המערכת
    SaveAdminLog("מחיקת משתמש", "נמחק חשבון משתמש מהמערכת (ID: " + data.Id + ")");

    return "חשבון המשתמש הוסר בהצלחה מהמערכת וכל הנתונים הקשורים אליו נמחקו.";
}

// פונקציה לעדכון פרטי משתמש במסד הנתונים
string UpdateAdminUser(UpdateAdminUserRequest data)
{
    // בדיקת אבטחה אם הטוקן אינו שייך למנהל חוסמים את הפעולה
    if (CheckIsAdmin(data.Token) == false)
    {
        return "גישה נדחתה, אין לך את ההרשאות המתאימות לביצוע הפעולה המבוקשת.";
    }

    // פתיחת צינור התקשורת למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // משפט SQL לעדכון השדות לפי ה-ID של המשתמש
    string sql = "UPDATE users SET full_name = @name, username = @user, email = @email WHERE id = @id";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@id", data.Id);
    command.Parameters.AddWithValue("@name", data.Full_name);
    command.Parameters.AddWithValue("@user", data.Username);
    command.Parameters.AddWithValue("@email", data.Email);

    // ביצוע פקודת העדכון במסד הנתונים
    command.ExecuteNonQuery();

    // שמירת פעולת העריכה ביומן הפעולות של המערכת
    SaveAdminLog("עריכת משתמש", "נערכו פרטי משתמש (ID: " + data.Id + ") שם: " + data.Full_name);

    return " פרטי החשבון עודכנו, כלל השינויים שבוצעו ברשומת המשתמש נשמרו בהצלחה";
}

// פונקציה לשמירת שורת פעולה חדשה ביומן המנהל במסד הנתונים
void SaveAdminLog(string actionType, string details)
{
    // פתיחת צינור תקשורת מול מסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // פקודת SQL להכנסת שורה חדשה לטבלת admin_logs
    // התאריך נכנס לבד אוטומטית
    string sql = "INSERT INTO admin_logs (action_type, details) VALUES (@type, @details)";
    using var command = new NpgsqlCommand(sql, connection);

    command.Parameters.AddWithValue("@type", actionType);
    command.Parameters.AddWithValue("@details", details);

    // הרצת הפקודה ושמירת השורה בפועל
    command.ExecuteNonQuery();
}

// פונקציה לשליפת יומן הפעולות האחרונות ממסד הנתונים
// שימוש ב IResult מאפשר להחזיר שגיאת אבטחה 401 או הצלחה 200 עם רשימת היומן
IResult GetAdminLogs(string token = "")
{
    // בדיקת אבטחה אם הטוקן אינו שייך למנהל חוסמים מיד את הבקשה
    if (CheckIsAdmin(token) == false)
    {
        // החזרת קוד שגיאה 401 Unauthorized שאומר לדפדפן שאין הרשאה מתאימה
        return Results.Unauthorized();
    }

    // רשימה ריקה שנמלא ונחזיר רק לאחר שהאימות עבר בהצלחה
    var list = new List<AdminLogItem>();

    // פתיחת צינור התקשורת מסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // שולף את הפעולות מהחדשה ביותר לישנה ביותר (ORDER BY id DESC)
    string sql = "SELECT id, action_type, details, created_at FROM admin_logs ORDER BY id DESC LIMIT 100";
    using var command = new NpgsqlCommand(sql, connection);
    using var reader = command.ExecuteReader(); // מריץ 

    // לולאה שעוברת שורה שורה וממלאת את הרשימה
    while (reader.Read())
    {
        var item = new AdminLogItem();
        item.id = reader.GetInt32(0);
        item.action_type = reader.GetString(1);
        item.details = reader.GetString(2);
        item.created_at = reader.GetDateTime(3).ToString("dd/MM/yyyy HH:mm");

        list.Add(item);
    }

    // החזרת יומן הפעולות עטוף בקוד סטטוס 200 OK והמרה אוטומטית לפורמט גייסון
    return Results.Ok(list);
}

// פונקציית אבטחה שמוודאת שהטוקן שייך למנהל
bool CheckIsAdmin(string token)
{
    // אם לא נשלח טוקן
    if (token == "")
    {
        return false;
    }

    // הצפנת הטוקן שהתקבל לפני בדיקת הרשאות מנהל מול מסד הנתונים
    string hashedToken = HashPassword(token);

    // בדיקה במסד הנתונים האם הטוקן קיים בתוקף ושייך למנהל
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();  

    string sql = "SELECT is_admin FROM users WHERE auth_token = @token AND token_expires > NOW()";
    using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@token", hashedToken);

    using var reader = command.ExecuteReader();
    
    // אם לא נמצא משתמש כזה
    if (reader.Read() == false)
    {
        return false;
    }
    // מחזיר האם הוא מנהל (true או false)
    return reader.GetBoolean(0) == true;
}



// פותח את דף הבית הראשי של השרת ומפעיל את הפונקציה למעלה כדי להציג את המשפט
app.MapGet("/", GetStatus); 

app.MapPost("/api/register", RegisterUser); // מחבר את פונקציית ה-RegisterUser 

app.MapPost("/api/login", LoginUser); // מחבר את פונקציית ה-LoginUser 

app.MapPost("/api/check-reset", CheckUserForReset); // מחבר את פונקציית ה-CheckUserForReset 

app.MapPost("/api/verify-reset-code", VerifyResetCode); // מחבר את פונקציית ה-VerifyResetCode 

app.MapPost("/api/update-password", UpdatePassword); // מחבר את פונקציית ה-UpdatePassword

app.MapPost("/api/google-login", GoogleLogin); // מחבר את פונקציית ה-GoogleLogin

app.MapPost("/api/resend-code", ResendCode); // מחבר את פונקציית ה-ResendCode

app.MapPost("/api/verify-token", VerifyToken); // מחבר את פונקציית אימות הטוקן לדשבורד

app.MapPost("/api/logout", LogoutUser); // מחבר את פונקציית ההתנתקות המאובטחת

app.MapGet("/api/admin/users", GetAdminUsers);

app.MapPost("/api/admin/delete-user", DeleteAdminUser); 

app.MapPost("/api/admin/update-user", UpdateAdminUser); 

app.MapGet("/api/admin/logs", GetAdminLogs); 

//  חיבור סוקט לבדיקה שהשרת תקין
app.Map("/ws/status", ServerStatusSocket.CheckStatus);

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
    public string CaptchaToken { get; set; } = ""; // הטוקן שקיבלנו מהדפדפן
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
    public string Token { get; set; } = ""; // הטוקן שנוצר באימות הקוד
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

// מחלקה לקליטת טוקן האימות מהדשבורד
class TokenCheck
{
    public string Token { get; set; } = "";
}

// מחלקה שמייצגת משתמש עבור דף המנהל
class AdminUser
{
    public int id { get; set; }
    public string full_name { get; set; } = "";
    public string username { get; set; } = "";
    public string email { get; set; } = "";
    public string password_hash { get; set; } = "";
    public bool is_verified { get; set; }
    public string reset_code_hash { get; set; } = "";
    public string reset_code_expires { get; set; } = "";
    public string auth_token { get; set; } = "";
    public string token_expires { get; set; } = "";
    }

// מחלקה לקליטת מזהה משתמש לצורך מחיקה
class DeleteUserRequest
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
}

// מחלקה לקליטת נתוני עדכון משתמש מדף הניהול
class UpdateAdminUserRequest
{
    public int Id { get; set; }
    public string Full_name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
}

// מחלקה שמייצגת שורת פעולה ביומן עבור דף המנהל
class AdminLogItem
{
    public int id { get; set; }
    public string action_type { get; set; } = "";
    public string details { get; set; } = "";
    public string created_at { get; set; } = "";
}
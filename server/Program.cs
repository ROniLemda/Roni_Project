using Npgsql; // פותח את ארגז הכלים של פוסטגרס ומאפשר לנו להשתמש בפקודות החיבור והטבלאות בקובץ הזה
// פותח את תיקיית הסייבר וההצפנה של סי שארפ כדי שנוכל להשתמש במנוע ה-SHA256
using System.Security.Cryptography;
using System.Text;
using System.Text.Json; // הכלים של json
using Microsoft.AspNetCore.Cors.Infrastructure; // כלים של הcors

// מפעיל את מנוע השרת של סי שארפ ומייצר את המחסן הראשי לבילדר
var builder = WebApplication.CreateBuilder(args);

// שליפת פרטי החיבור של מסד הנתונים מתוך קובץ ההגדרות
var databaseAddress = builder.Configuration.GetConnectionString("DefaultConnection");

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

//  פונקציה שמקבלת את טופס נתוני הרישום מהאתר ומחזירה הודעת טקסט שההרשמה הצליחה
string RegisterUser(UserRegister data)
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
    string checkSql = "SELECT id FROM users WHERE email = @email OR username = @user";
        using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("@email", data.Email);
        checkCmd.Parameters.AddWithValue("@user", data.Username);

        using var checkReader = checkCmd.ExecuteReader();
        if (checkReader.Read() == true)
        {
            return "שם המשתמש או כתובת האימייל שהוזנו כבר קיימים במערכת, אנא בחר פרטים אחרים.";
        }
        checkReader.Close(); // סוגרים את הקורא כדי שנוכל להמשיך להרשמה


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
    return "נרשמת בהצלחה, קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך לצורך הפעלת החשבון.";
}

string LoginUser(UserLogin data)
{
    // מצפין את הסיסמה שהמשתמש הקליד עכשיו כדי להשוות למה ששמור בטבלה
    string hashPass = HashPassword(data.Password);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // משפט SQL שמחפש משתמש עם האימייל הזה ושולף את הסיסמה המוצפנת שלו
    string sql = "SELECT password_hash FROM users WHERE email = @loginInput OR username = @loginInput";  
      using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@loginInput", data.Email);
    
    // מריצים את הפקודה ומקבלים את הנתונים 
    using var reader = command.ExecuteReader();

    // האם פוסטגרס מצא שורה בכלל
    if (reader.Read() == false)
    {
        return "המשתמש אינו קיים במערכת בדוק את פרטי ההתחברות ונסה שנית.";
    }

    // שולפים את הסיסמה המוצפנת שנשמרה בעבר בטבלה
    string savedPassword = reader.GetString(0);

    // האם הסיסמה שהוקלדה עכשיו מתאימה למה ששמור
    if (hashPass == savedPassword)
    {
        return "ההתחברות בוצעה בהצלחה, מיד תועבר לסביבת העבודה שלך.";
    }
    else
    {
        return "שם המשתמש או הסיסמה שגויים, אנא בדוק את הפרטים ונסה שוב.";  
    }
}

string CheckUserForReset(UserReset data)
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

    return "קוד אימות בן 6 ספרות נשלח לכתובת האימייל שלך, אנא הזן אותו בכדי להמשך.";
}


string VerifyResetCode(VerifyCodeRequest data)
{
    //  מצפינים את הקוד שהמשתמש הקליד עכשיו כדי להשוות ל-Hash ששמור
    string hashedInputCode = HashPassword(data.Code);

    // פתיחת חיבור למסד הנתונים
    using var connection = new NpgsqlConnection(databaseAddress);
    connection.Open();

    // שליפת הקוד השמור וזמן התפוגה לפי האימייל
    string sql = "SELECT reset_code_hash, reset_code_expires FROM users WHERE email = @email";
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
    // מחיקת קוד האימות מהטבלה שלא ישתמשו בו שוב
    string clearCodeSql = "UPDATE users SET reset_code_hash = NULL, reset_code_expires = NULL WHERE email = @email";
    using var clearCmd = new NpgsqlCommand(clearCodeSql, connection);
    clearCmd.Parameters.AddWithValue("@email", data.Email);
    clearCmd.ExecuteNonQuery();
        
    return "זהותך אומתה בהצלחה, הנך מועבר כעת לקביעת הסיסמה החדשה.";
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
    string insertSql = "INSERT INTO users (full_name, username, email, password_hash) VALUES (@name, @user, @email, 'GOOGLE_AUTH')";
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
using System.Net.WebSockets; // פותח את ארגז הכלים של תקשורת סוקט בסי שארפ
using System.Text; // מאפשר המרה בין טקסט לבתים של מידע ברשת
// ברשת האינטרנט ובסוקט מידע עובר בתור בתים

// מחלקה שמנהלת את כל חיבורי הסוקט של תקינות השרת
public static class ServerStatusSocket
{
     // פעולה שמטפלת בבדיקת הסטטוס דרך הסוקט
    public static async Task CheckStatus(HttpContext http)
    {
        // בדיקה האם הפנייה שמגיעה מהדפדפן היא בקשת סוקט
        if (http.WebSockets.IsWebSocketRequest == true)
        {
            // רשימת הכתובות המורשות של האתר
            string[] allowedSites = { "http://localhost:5500", "http://127.0.0.1:5500" };

            // שליפת כתובת האתר שממנו הגיעה הפנייה של המשתמש
            string userOrigin = http.Request.Headers["Origin"].ToString();

            // משתנה דגל שבודק האם הכתובת נמצאה ברשימה המורשת
            bool isAllowed = false;

            // לולאת פור שעוברת על כל הכתובות במערך
            for (int i = 0; i < allowedSites.Length; i++)
            {
                // בדיקה האם הכתובת הנוכחית במערך תואמת לכתובת של המשתמש
                if (allowedSites[i] == userOrigin)
                {
                    // סימון הדגל כחיובי אם נמצאה התאמה
                    isAllowed = true;
                }
            }

            // אם הכתובת אינה מורשת חוסמים את הגישה לסוקט
            if (isAllowed == false)
            {
                // החזרת קוד שגיאה 403 של חוסר הרשאה
                http.Response.StatusCode = 403;
                return;
            }

            // אישור בקשת הסוקט ופתיחת צינור תקשורת ישיר מול הדפדפן
            using var socket = await http.WebSockets.AcceptWebSocketAsync();

            // מערך בתים שיקלוט את הנתונים שמגיעים בסוקט
            byte[] data = new byte[1024];

            // בלוק ניסיון להאזנה לחבילות המידע בסוקט
            try
            {
                // לולאה שממשיכה לרוץ כל עוד החיבור בסוקט פתוח ותקין
                while (socket.State == WebSocketState.Open)
                {
                    // המתנה לקבלת מידע מהדפדפן בסוקט ושמירה במערך
                    var info = await socket.ReceiveAsync(data, default);

                    // בדיקה האם הדפדפן ביקש לנתק או לסגור את הסוקט בצורה מסודרת
                    if (info.MessageType == WebSocketMessageType.Close)
                    {
                        // סגירה של החיבור מצד השרת ושחרור הקו
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "סגירה", default);
                    }
                    else
                    {
                        // המרת הבתים שהתקבלו למחרוזת טקסט רגילה
                        string message = Encoding.UTF8.GetString(data, 0, info.Count);

                        // אם הדפדפן שלח בדיקת דופק
                        if (message == "PING")
                        {
                            // המרת התשובה למערך בתים לשליחה ברשת
                            byte[] response = Encoding.UTF8.GetBytes("PONG");

                            // שליחת התשובה חזרה לדפדפן דרך הסוקט
                            await socket.SendAsync(response, WebSocketMessageType.Text, true, default);
                        }
                    }
                }
            }
            // תופס שגיאת ניתוק רשת פתאומי במקרה שהמשתמש סגר את הלשונית בדפדפן ריענן את הדף או נותק מהאינטרנט
            catch (WebSocketException)
            {
                
            }
        }
        else
        {
            // אם זו פנייה רגילה ולא סוקט נחזיר שגיאה של בקשה לא תקינה
            http.Response.StatusCode = 400;
        }
    }
}
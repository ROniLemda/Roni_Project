// כתובת השרת המרכזית של האתר
const API_URL = 'http://localhost:5057';

// מחלקת Auth - מחלקה לבדיקה מרכזית של כל האתר (התחברות, הרשמה ואימות)
class Auth 
{
    // בדיקת אימייל או שם משתמש בהתחברות (בודק רק שלא ריק)
    static checkLoginUser(input) 
    {
        if (input.trim() === '') 
        {
            return 'נא להזין כתובת אימייל או שם משתמש';
        }
        return '';
    }

    // בדיקת אימייל בהרשמה (חייב אימייל אמיתי עם @)
    static checkEmail(email) 
    {
        if (email.trim() === '') 
        {
            return 'נא להזין כתובת אימייל';
        }
        if (email.includes('@') === false) 
        {
            return 'כתובת אימייל תקנית חייבת להכיל @';
        }
        return ''; // אם הכל בסדר מחזיר ריק
    }

        // בדיקת סיסמה
    static checkPassword(password) 
    {
        if (password.trim() === '') 
        {
            return 'נא להזין סיסמה';
        }

        if (password.length < 6) 
        {
            return 'הסיסמה חייבת להכיל לפחות 6 תווים';
        }

        // בדיקה שיש לפחות אות גדולה אחת באנגלית
        if (/[A-Z]/.test(password) === false) 
        {
            return 'הסיסמה חייבת להכיל לפחות אות גדולה אחת באנגלית';
        }

        // בדיקה שיש לפחות תו מיוחד אחד
        if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password) === false) 
        {
            return 'הסיסמה חייבת להכיל לפחות תו מיוחד אחד (!@#$%&*)';
        }
        return ''; // אם הכל בסדר מחזיר ריק
    }

        // בדיקת שם משתמש
    static checkUsername(username) 
    {
        if (username.trim() === '') 
        {
            return 'נא להזין שם משתמש';
        }

        if (username.length < 3) 
        {
            return 'שם משתמש חייב להכיל לפחות 3 תווים';
        }

        return ''; // אם הכל בסדר מחזיר ריק
    }

        // בדיקת שם מלא
    static checkFullName(fullName) 
    {
        if (fullName.trim() === '') 
        {
            return 'נא להזין שם מלא';
        }

        if (fullName.trim().length < 2) 
        {
            return 'שם מלא חייב להכיל לפחות 2 אותיות';
        }

        return ''; // אם הכל בסדר מחזיר ריק
    }

        // בדיקת קוד אימות 6 ספרות
    static checkOtpCode(code) 
    {
        if (code.trim() === '') 
        {
            return 'נא להזין את קוד האימות';
        }

        if (code.length < 6) 
        {
            return 'יש להזין את כל 6 הספרות של הקוד';
        }
        return '';
    }
    
    // מציג או מסתיר את הסיסמה
    static togglePassword(input, icon) 
    {
        if (input.type === 'password') 
        {
            input.type = 'text'; // הופך לטקסט גלוי
            icon.classList.replace('fa-eye', 'fa-eye-slash'); // שם עין עם קו
        } 
        else 
        {
            input.type = 'password'; // מחזיר לנקודות שחורות
            icon.classList.replace('fa-eye-slash', 'fa-eye'); // מחזיר עין רגילה
        }
    }
}

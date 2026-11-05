namespace UnoOrbit.Models;

public record PlanetData(
    string Name,
    string Type,
    string ColorHex,
    float OrbitRadius,
    float Size,
    float Speed,
    string Diameter,
    string Mass,
    string Distance,
    string DayLength,
    string Gravity,
    string Temperature,
    string Atmosphere,
    string AtmosphereColorRgba,
    string[] Facts,
    string[] FactIcons
);

public static class Planets
{
    public static readonly PlanetData[] All =
    [
        new("Mercury", "Terrestrial Planet", "#b0a090", 0.08f, 4f, 4.15f,
            "4,879 km", "3.30 \u00d7 10\u00b2\u00b3 kg", "57.9M km", "58.6 days",
            "3.7 m/s\u00b2", "167\u00b0C avg", "Virtually none",
            "rgba(176,160,144,0.15)",
            ["A year on Mercury is just 88 Earth days, but a single day lasts 59 Earth days.",
             "Mercury has no atmosphere, so temperatures swing from -180\u00b0C at night to 430\u00b0C during the day.",
             "Mercury is shrinking! It has contracted by more than 14 km in diameter over billions of years."],
            ["\ud83c\udf21\ufe0f", "\ud83c\udf21\ufe0f", "\ud83d\udd2d"]),

        new("Venus", "Terrestrial Planet", "#e6c88a", 0.14f, 7f, 1.62f,
            "12,104 km", "4.87 \u00d7 10\u00b2\u2074 kg", "108.2M km", "243 days",
            "8.9 m/s\u00b2", "464\u00b0C avg", "96.5% CO\u2082, 3.5% N\u2082",
            "rgba(230,200,138,0.12)",
            ["Venus rotates backwards compared to most planets, and a day on Venus is longer than its year.",
             "The atmospheric pressure on Venus is 90 times that of Earth, enough to crush a submarine.",
             "Venus is the hottest planet in the solar system despite being farther from the Sun than Mercury."],
            ["\u2604\ufe0f", "\u2604\ufe0f", "\ud83d\udd25"]),

        new("Earth", "Terrestrial Planet", "#4da6ff", 0.20f, 8f, 1.0f,
            "12,742 km", "5.97 \u00d7 10\u00b2\u2074 kg", "149.6M km", "24 hours",
            "9.8 m/s\u00b2", "15\u00b0C avg", "78% N\u2082, 21% O\u2082",
            "rgba(77,166,255,0.12)",
            ["Earth is the only planet not named after a Greek or Roman god.",
             "Earth's rotation is gradually slowing, making days about 1.4 milliseconds longer per century.",
             "About 71% of Earth's surface is covered in water, yet 95% of the oceans remain unexplored."],
            ["\ud83c\udf0d", "\u231b", "\ud83c\udf0a"]),

        new("Mars", "Terrestrial Planet", "#e07040", 0.27f, 6f, 0.53f,
            "6,779 km", "6.42 \u00d7 10\u00b2\u00b3 kg", "227.9M km", "24.6 hours",
            "3.7 m/s\u00b2", "-65\u00b0C avg", "95.3% CO\u2082, 2.7% N\u2082",
            "rgba(224,112,64,0.12)",
            ["Mars has the tallest volcano in the solar system - Olympus Mons at 21.9 km high.",
             "Mars has two small moons, Phobos and Deimos, which may be captured asteroids.",
             "Sunsets on Mars appear blue due to how dust particles scatter light in the atmosphere."],
            ["\ud83c\udf0b", "\ud83c\udf11", "\ud83c\udf05"]),

        new("Jupiter", "Gas Giant", "#d4a574", 0.38f, 18f, 0.084f,
            "139,820 km", "1.90 \u00d7 10\u00b2\u2077 kg", "778.5M km", "9.93 hours",
            "23.1 m/s\u00b2", "-110\u00b0C avg", "90% H\u2082, 10% He",
            "rgba(212,165,116,0.10)",
            ["Jupiter's Great Red Spot is a storm that has been raging for at least 350 years.",
             "Jupiter has at least 95 known moons, including the four large Galilean moons.",
             "Jupiter's magnetic field is 20,000 times stronger than Earth's."],
            ["\ud83c\udf00", "\ud83e\ude90", "\u26a1"]),

        new("Saturn", "Gas Giant", "#e8d5a0", 0.50f, 15f, 0.034f,
            "116,460 km", "5.68 \u00d7 10\u00b2\u2076 kg", "1.43B km", "10.7 hours",
            "9.0 m/s\u00b2", "-140\u00b0C avg", "96.3% H\u2082, 3.25% He",
            "rgba(232,213,160,0.10)",
            ["Saturn's density is so low that it would float in water if you could find a bathtub big enough.",
             "Saturn's rings are mostly made of ice particles ranging from tiny grains to house-sized chunks.",
             "Winds on Saturn can reach speeds of 1,800 km/h near the equator."],
            ["\ud83d\udcab", "\ud83d\udc8e", "\ud83c\udf2c\ufe0f"]),

        new("Uranus", "Ice Giant", "#7ec8c8", 0.63f, 11f, 0.012f,
            "50,724 km", "8.68 \u00d7 10\u00b2\u2075 kg", "2.87B km", "17.2 hours",
            "8.7 m/s\u00b2", "-195\u00b0C avg", "83% H\u2082, 15% He, 2% CH\u2084",
            "rgba(126,200,200,0.10)",
            ["Uranus rotates on its side with an axial tilt of 98 degrees, likely from an ancient collision.",
             "Uranus was the first planet discovered using a telescope, by William Herschel in 1781.",
             "A season on Uranus lasts 21 Earth years due to its extreme axial tilt."],
            ["\ud83d\udd35", "\ud83d\udd2d", "\u2744\ufe0f"]),

        new("Neptune", "Ice Giant", "#4466dd", 0.76f, 10f, 0.006f,
            "49,528 km", "1.02 \u00d7 10\u00b2\u2076 kg", "4.50B km", "16.1 hours",
            "11.0 m/s\u00b2", "-200\u00b0C avg", "80% H\u2082, 19% He, 1% CH\u2084",
            "rgba(68,102,221,0.10)",
            ["Neptune has the strongest winds in the solar system, reaching speeds of 2,100 km/h.",
             "Neptune takes 165 Earth years to orbit the Sun, completing its first full orbit since discovery in 2011.",
             "Neptune's moon Triton orbits backwards and is thought to be a captured Kuiper Belt object."],
            ["\ud83c\udf2c\ufe0f", "\u23f0", "\ud83c\udf11"]),
    ];
}

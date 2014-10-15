using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.IO.Ports;
using System.Threading;

namespace MIDIPlaySerial {
    class Program {
        //http://flashmusicgames.com/midi/mid2xml.php
        static void Main(string[] args) {
            double timescale = 1.0;
            string fname = "stairway.xml";
            List<Instrument> insts = new List<Instrument>();
            switch (fname) {
                case "sweetchild.xml":
                    insts.Add(new Instrument(0, 1).setPitchshift(-12)); // lead
                    insts.Add(new Instrument(0, 5)); // harmony
                    insts.Add(new Instrument(1, 15)); // bass
                    insts.Add(new Instrument(2, 10).setNotefilter(40).setDuration(100)); // snare
                    insts.Add(new Instrument(3, 10).setNotefilter(36).setDuration(70)); // kick
                    break;
                case "aintno.xml":
                    insts.Add(new Instrument(0, 16).setPitchshift(-12));
                    insts.Add(new Instrument(1, 1));
                    break;
                case "thunderstruck.xml":
                    insts.Add(new Instrument(0, 1));
                    insts.Add(new Instrument(1, 2));
                    insts.Add(new Instrument(1, 3));
                    insts.Add(new Instrument(2, 10).setNotefilter(47).setDuration(100)); // snare
                    insts.Add(new Instrument(3, 10).setNotefilter(35).setDuration(70)); // kick
                    break;
                case "stairway.xml":
                    insts.Add(new Instrument(0, 2)); 
                    insts.Add(new Instrument(1, 2));
                    insts.Add(new Instrument(2, 10).setNotefilter(40).setDuration(100)); // snare
                    insts.Add(new Instrument(3, 10).setNotefilter(35).setDuration(70)); // kick
                    insts.Add(new Instrument(0, 11).setPitchshift(-12));
                    insts.Add(new Instrument(0, 1).setPitchshift(-12)); // harmonica
                    insts.Add(new Instrument(1, 4)); // how many guitars are there?
                    insts[0].preferHigh = true;
                    insts[1].preferLow = true;
                    timescale = 0.3;
                    break;
                case "weeps.xml":
                    insts.Add(new Instrument(0, 3)); insts[0].preferHigh = true;
                    insts.Add(new Instrument(1, 5));
                    insts.Add(new Instrument(2, 10).setNotefilter(38).setDuration(100)); // snare
                    insts.Add(new Instrument(3, 10).setNotefilter(36).setDuration(70)); // kick
                    timescale = 0.3;
                    break;
            }

            int jumpto = 0;

            foreach (Instrument i in insts) {
                if (i.voice == 2) i.timeshift = -70;
                if (i.voice == 3) i.timeshift = -70;
            }
            int Nvoices = 8;

            XmlDocument doc = new XmlDocument();
            doc.Load(fname);
            XmlNode root = doc.SelectSingleNode("MIDIFile");
            List<Event> events = new List<Event>();

            byte[] voices = new byte[Nvoices];
            foreach (XmlNode note in root.SelectNodes("Track/Event/NoteOn | Track/Event/NoteOff")) {
                int channel = Convert.ToInt32(note.Attributes["Channel"].Value);
                int pitch = Convert.ToInt32(note.Attributes["Note"].Value);
                int when = (int)(Convert.ToInt32(note.ParentNode.SelectSingleNode("Absolute").InnerText) / timescale);
                int velocity = (int)(Convert.ToInt32(note.Attributes["Velocity"].Value));

                List<Instrument> matches = new List<Instrument>();
                foreach (Instrument i in insts) {
                    if (i.chan != channel) continue;
                    if (i.notefilter != 0 && i.notefilter != pitch) continue;
                    matches.Add(i);
                }
                foreach (Instrument inst in matches) {
                    int voice = inst.voice;
                    pitch += inst.pitchshift;
                    when += inst.timeshift;

                    if (note.Name == "NoteOn" && velocity > 0) {
                        if (voices[voice] == 0 ||
                            (inst.preferLow && pitch < voices[voice]) ||
                            (inst.preferHigh && pitch > voices[voice])) {
                            voices[voice] = (byte)pitch;
                            events.Add(new Event(when, voice, pitch));
                            if (inst.duration != 0) {
                                // artificial duration limits
                                events.Add(new Event(when + inst.duration, voice, 0));
                                voices[voice] = 0;
                            }
                        }
                    } else {
                        if (voices[voice] == pitch) {
                            voices[voice] = 0;
                            events.Add(new Event(when, voice, 0));
                        }
                    }
                }
            }
            events.Sort(delegate(Event a, Event b) {
                int res = a.time.CompareTo(b.time);
                return res != 0 ? res : a.pitch.CompareTo(b.pitch); // noteoff before noteon
            });

            for (int i = 0; i < Nvoices; i++)
                voices[i] = 0;

            /* write events to file for RPi playback */
            StreamWriter sw = new StreamWriter(fname.Replace(".xml", ".rpi"));
            int lasttime = 0;
            for (int ei = 0; ei < events.Count; ei++) {
                Event e = events[ei];

                lasttime = e.time;
                voices[e.voice] = (byte)e.pitch;
                if (ei + 1 < events.Count && events[ei + 1].time == e.time) 
                    continue; // don't write yet, more changes on this tick
                sw.WriteLine(e.time + " " + String.Join(" ", voices));
            }
            sw.Close();

            return;

            SerialPort sp = new SerialPort("COM1", 9600);
            sp.Open();

            Console.CancelKeyPress += delegate {
                for (int i = 0; i < Nvoices; i++)
                    voices[i] = 0;
                sp.Write(voices, 0, 8);

                sp.Close();
            };


            lasttime = 0;
            DateTime start = DateTime.MinValue;
            for (int ei = 0; ei < events.Count; ei++) {
                Event e = events[ei];
                if (e.time < jumpto)
                    continue;
                else if (start == DateTime.MinValue)
                    start = DateTime.Now - TimeSpan.FromMilliseconds(e.time);

                if (e.time != lasttime) {
                    int elapsed = (int)((DateTime.Now - start).TotalMilliseconds);
                    if (elapsed > e.time)
                        d("behind " + (elapsed - e.time) + " msec");
                    else
                        Thread.Sleep(e.time - elapsed);
                }
                lasttime = e.time;
                voices[e.voice] = (byte)e.pitch;
                if (ei + 1 < events.Count && events[ei + 1].time == e.time) 
                    continue; // don't write yet, more changes on this tick
                d("@" + e.time + " " + e.voice + " pitch " + e.pitch + " = " + String.Join(" ", voices));
                sp.Write(voices, 0, Nvoices);
            }

            for (int i = 0; i < Nvoices; i++)
                voices[i] = 0;
            sp.Write(voices, 0, 8);

            sp.Close();
        }


        static void d(string msg) { Console.WriteLine(msg); }
    }
    public class Event {
        public int time, voice, pitch;
        public Event(int time, int voice, int pitch) {
            this.time = time; this.voice = voice; this.pitch = pitch;
        }
    }
    public class Instrument {
        public int voice, chan, timeshift, pitchshift, notefilter, duration;
        public bool preferLow, preferHigh;
        public Instrument(int voice, int chan) {
            this.voice = voice; this.chan = chan;
        }
        public Instrument setPitchshift(int pitchshift) {
            this.pitchshift = pitchshift;
            return this;
        }

        public Instrument setNotefilter(int notefilter) {
            this.notefilter = notefilter;
            return this;
        }

        public Instrument setDuration(int duration) {
            this.duration = duration;
            return this;
        }
    }
}

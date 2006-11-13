/* -*- Mode: C; tab-width: 8; indent-tabs-mode: nil; c-basic-offset: 8 -*- */

/*
 * outfile-writer.c
 *
 * Copyright (C) 2005 Novell, Inc.
 *
 */

/*
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of version 2 of the GNU General Public
 * License as published by the Free Software Foundation.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
 * USA.
 */

#include <string.h>
#include <time.h>

#include "outfile-writer.h"

#define MAGIC_NUMBER 0x4eabfdd1
#define FILE_FORMAT_VERSION 6
#define FILE_LABEL "heap-shot logfile"
#define TAG_TYPE    0x01
#define TAG_OBJECT  0x02
#define TAG_EOS     0xff

static void
write_byte (FILE *out, guint8 x)
{
        fwrite (&x, sizeof (guint8), 1, out);
}

static void
write_pointer (FILE *out, gpointer x)
{
        guint32 y = GPOINTER_TO_UINT (x);
        fwrite (&y, sizeof (guint32), 1, out);
}

static void
write_int32 (FILE *out, gint32 x)
{
        fwrite (&x, sizeof (gint32), 1, out);
}

static void
write_uint32 (FILE *out, guint32 x)
{
        fwrite (&x, sizeof (guint32), 1, out);
}

static void
write_vint (FILE *out, guint32 x)
{
        guint8 y;

        do {
                y = (guint8) (x & 0x7f);
                x = x >> 7;
                if (x != 0)
                        y |= 0x80;
                write_byte (out, y);
        } while (x != 0);
}

static void
write_string (FILE *out, const char *str)
{
        int len = strlen (str);
        write_vint (out, (guint32) len);
        fwrite (str, sizeof (char), len, out);
}

OutfileWriter *
outfile_writer_open_objectmap (const char *filename)
{
        OutfileWriter *ofw;

        ofw = g_new0 (OutfileWriter, 1);
        ofw->out = fopen (filename, "w");
        ofw->seen_items = g_hash_table_new (NULL, NULL);

        write_uint32 (ofw->out, MAGIC_NUMBER);
        write_int32  (ofw->out, FILE_FORMAT_VERSION);
        write_string (ofw->out, FILE_LABEL);
        
        ofw->saved_outfile_offset = ftell (ofw->out);
        // we update these after dumping all objects
        write_int32 (ofw->out, 0);   // total # of types
        write_int32 (ofw->out, 0);   // total # of objects
        write_int32 (ofw->out, 0); // total # of references
        write_int32 (ofw->out, 0); // total # of fields

        return ofw;
}

void
outfile_writer_close (OutfileWriter *ofw)
{
        // Write out the end-of-stream tag.
        write_byte (ofw->out, TAG_EOS);
        
        // Seek back up to the right place in the header
        fseek (ofw->out, ofw->saved_outfile_offset, SEEK_SET);
        write_int32 (ofw->out, ofw->type_count);
        write_int32 (ofw->out, ofw->object_count);
        write_int32 (ofw->out, ofw->reference_count);
        write_int32 (ofw->out, ofw->field_count);
        
        fclose (ofw->out);
}

void
outfile_writer_dump_object_begin (OutfileWriter *ofw,
                                  MonoObject    *obj,
                                  MonoClass     *klass)
{
        char *name;
		
        /* First, add this type if we haven't seen it before. */
        if (g_hash_table_lookup (ofw->seen_items, klass) == NULL) {
				MonoClassField *field;
				gpointer iter = NULL;
				
                name = mono_type_full_name (mono_class_get_type (klass));
                write_byte (ofw->out, TAG_TYPE);
                write_pointer (ofw->out, klass);
                write_string (ofw->out, name);
                g_free (name);
                g_hash_table_insert (ofw->seen_items, klass, klass);
                ++ofw->type_count;
                
                // Write every field
				while ((field = mono_class_get_fields (klass, &iter)) != NULL) {
	                write_pointer (ofw->out, field);
	                write_string (ofw->out, mono_field_get_name (field));
	                ofw->field_count++;
				}
                write_pointer (ofw->out, NULL);
        }
        write_byte (ofw->out, TAG_OBJECT);
        if (obj) {
	        write_pointer (ofw->out, (gpointer)obj);	// id of the object
    		write_pointer (ofw->out, klass);			// class
			write_int32 (ofw->out, (gint32)mono_object_get_size (obj));	// size of the object
		} else {
			// Used to register references from static class members
	        write_pointer (ofw->out, (gpointer)klass);
    		write_pointer (ofw->out, klass);
			write_int32 (ofw->out, (gint32)0);
		}
		ofw->object_count++;
}

void
outfile_writer_dump_object_end (OutfileWriter *ofw)
{
        write_pointer (ofw->out, NULL);	// no more references
}

void
outfile_writer_dump_object_add_reference (OutfileWriter *ofw, gpointer ref, gpointer field)
{
        write_pointer (ofw->out, ref);
        write_pointer (ofw->out, field);
        ofw->reference_count++;
}


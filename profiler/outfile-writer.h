/* -*- Mode: C; tab-width: 8; indent-tabs-mode: nil; c-basic-offset: 8 -*- */

/*
 * outfile-writer.h
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

#ifndef __OUTFILE_WRITER_H__
#define __OUTFILE_WRITER_H__

#include <glib.h>
#include <mono/metadata/class.h>
#include <mono/metadata/object.h>

typedef struct _OutfileWriter OutfileWriter;
struct _OutfileWriter {
        FILE *out;
        GHashTable *seen_items;
        int type_count;
};

OutfileWriter *outfile_writer_open_objectmap (const char *filename);

void           outfile_writer_close          (OutfileWriter *ofw);

void           outfile_writer_dump_object_begin (OutfileWriter *ofw,
                                                 MonoObject    *obj,
                                                 MonoClass     *klass);

void           outfile_writer_dump_object_end (OutfileWriter *ofw);

void           outfile_writer_dump_object_add_reference (OutfileWriter *ofw,
                                                         gpointer ref,
                                                         gpointer field);



#endif /* __OUTFILE_WRITER_H__ */


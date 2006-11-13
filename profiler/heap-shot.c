/* -*- Mode: C; tab-width: 8; indent-tabs-mode: t; c-basic-offset: 8 -*- */

/*
 * heap-shot.c
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
#include <glib.h>
#include <mono/metadata/assembly.h>
#include <mono/io-layer/mono-mutex.h>
#include <mono/metadata/class.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/object.h>
#include <mono/metadata/profiler.h>
#include <mono/metadata/mono-gc.h>
#include <mono/metadata/metadata.h>
#include <mono/metadata/debug-helpers.h>
#include <unistd.h>
#include <time.h>
#include <signal.h>

#include "outfile-writer.h"

//extern gboolean mono_object_is_alive (MonoObject* obj);

struct _MonoProfiler {
	mono_mutex_t   lock;
	mono_mutex_t   dump_lock;
	GHashTable    *objects_hash;
	GHashTable    *class_hash;
	GHashTable    *exclude_class_hash;
	GHashTable    *work_objects_hash;
	GHashTable    *work_class_hash;
	OutfileWriter *dumpfile_writer;
	const char    *out_file_name;
	gint           dump_count;
};

static MonoProfiler* prof;

static void heap_shot_dump_object_map (MonoProfiler *p);


static void
profiler_signal_handler (int nsig)
{
	heap_shot_dump_object_map (prof);
}


static MonoProfiler *
create_mono_profiler (const char *outfilename)
{
	struct sigaction sa;
	MonoProfiler *p = g_new0 (MonoProfiler, 1);
	prof = p;

	mono_mutex_init (&p->lock, NULL);
	mono_mutex_init (&p->dump_lock, NULL);

	p->objects_hash     = g_hash_table_new (NULL, NULL);
	p->class_hash       = g_hash_table_new (NULL, NULL);
	p->exclude_class_hash = g_hash_table_new (NULL, NULL);
	p->out_file_name    = outfilename;
	p->dump_count       = 0;

	// Sets the PROF signal
	sa.sa_handler = profiler_signal_handler;
	sigemptyset (&sa.sa_mask);
	sa.sa_flags = 0;
	g_assert (sigaction (SIGPROF, &sa, NULL) != -1);

	return p;
}

/* ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** ** */

static void
heap_shot_alloc_func (MonoProfiler *p, MonoObject *obj, MonoClass *klass)
{
	mono_mutex_lock (&p->lock);
	g_hash_table_insert (p->objects_hash, obj, klass);
	mono_mutex_unlock (&p->lock);
}

static gboolean
post_gc_clean_objects_fn (gpointer key, gpointer value, gpointer user_data)
{
	MonoObject *obj = key;
	return !mono_object_is_alive (obj);
}

static void
heap_shot_gc_func (MonoProfiler *p, MonoGCEvent e, int gen)
{
	if (e != MONO_GC_EVENT_MARK_END)
		return;

	mono_mutex_lock (&p->lock);
	g_hash_table_foreach_remove (p->objects_hash, post_gc_clean_objects_fn, NULL);
	mono_mutex_unlock (&p->lock);
}

static gboolean
heap_scan_object (MonoProfiler *p, MonoObject *obj, MonoClass *klass, MonoClassField *parent_field)
{
	gpointer iter = NULL;
	MonoClassField *field;
	gboolean has_refs = FALSE;

	while ((field = mono_class_get_fields (klass, &iter)) != NULL) {
		MonoType* field_type = mono_field_get_type (field);
		// Skip static fields
		if (mono_field_get_flags (field) & 0x0010 /*FIELD_ATTRIBUTE_STATIC*/)
			continue;

		if (MONO_TYPE_IS_REFERENCE (field_type)) {
			// Dump the object reference
			MonoObject* ref;
			has_refs = TRUE;
			mono_field_get_value (obj, field, &ref);
			if (ref && g_hash_table_lookup (p->work_objects_hash, ref))
				outfile_writer_dump_object_add_reference (p->dumpfile_writer, ref, parent_field);
		}
		else {
			MonoClass *fclass = mono_class_from_mono_type (field_type);
			if (fclass && mono_class_is_valuetype (fclass)) {
				if (g_hash_table_lookup (p->exclude_class_hash, fclass))
					continue;
				// It's a value type. Check if the class is big enough to hold references
				int size = mono_class_value_size (fclass, NULL);
				if (size >= sizeof(gpointer) && fclass != klass) {
					// Get the object value and scan it
					char* vop = g_malloc (size);
					mono_field_get_value (obj, field, vop);
					// Recursively scan the object
					if (heap_scan_object (p, (MonoObject*)(vop - sizeof(MonoObject)), fclass, parent_field))
						has_refs = TRUE;
					g_free (vop);
				}
			}
		}
	}
	// If the class doesn't contain references, register in the exclude_class_hash table,
	// so it won't be scanned again.
	if (!has_refs && !g_hash_table_lookup (p->exclude_class_hash, klass))
		g_hash_table_insert (p->exclude_class_hash, klass, klass);
	return has_refs;
}

static void
heap_scan_array (MonoProfiler *p, MonoObject *obj, MonoClass *klass)
{
	MonoArray *array = (MonoArray *) obj;
	MonoClass *eklass = mono_class_get_element_class (klass);
	gboolean has_refs = FALSE;
	
	if (!mono_class_is_valuetype (eklass)) {
		// It's an array of object references, write all of them in the output file
		int n;
		for (n=0; n<mono_array_length (array); n++) {
			MonoObject *ref = mono_array_get (array, MonoObject*, n);
			if (ref && g_hash_table_lookup (p->work_objects_hash, ref))
				outfile_writer_dump_object_add_reference (p->dumpfile_writer, ref, NULL);
		}
		has_refs = TRUE;
	}
	else if (!g_hash_table_lookup (p->exclude_class_hash, eklass)) {
		// It's an array of value type objects. Each object will be scanned
		// by recursively calling heap_scan_object for each member
		int n;
		gint32 esize = mono_array_element_size (klass);
		if (esize >= sizeof(gpointer)) {
			// The type is big enough to contain references.
			// Scan the array.
			for (n=0; n<mono_array_length (array); n++) {
				char *ref = (char *) mono_array_addr_with_size (array, esize, n);
				ref -= sizeof (MonoObject);
				if (heap_scan_object (p, (MonoObject *) ref, eklass, NULL))
					has_refs = TRUE;
				else
					// The class has no fields, it makes no sense to continue
					break;
			}
		}
	}
	// If the class doesn't contain references, register in the exclude_class_hash table,
	// so it won't be scanned again.
	if (!has_refs && !g_hash_table_lookup (p->exclude_class_hash, klass))
		g_hash_table_insert (p->exclude_class_hash, klass, klass);
}

static void
heap_scan_fn (gpointer key, gpointer value, gpointer user_data)
{
	MonoProfiler *p = user_data;
	MonoObject *obj = key;
	MonoClass  *klass = value;
	
	// Write the object header
	outfile_writer_dump_object_begin (p->dumpfile_writer, obj, klass);
	
	// If the type is registered as not having reference fields, just return
	if (g_hash_table_lookup (p->exclude_class_hash, klass)) {
		outfile_writer_dump_object_end (p->dumpfile_writer);
		return;
	}

	if (mono_class_get_rank (klass)) {
		// It's an array
		heap_scan_array (p, obj, klass);
	}
	else {
		heap_scan_object (p, obj, klass, NULL);
	}

	// Write the object end marker	
	outfile_writer_dump_object_end (p->dumpfile_writer);
}

static void
dump_static_fields_fn (gpointer key, gpointer value, gpointer user_data)
{
	MonoClassField *field;
	gpointer iter = NULL;
	gboolean added = FALSE;
	MonoClass *klass = key;
	MonoProfiler *p = ((gpointer*)user_data)[0];
	MonoDomain *domain = ((gpointer*)user_data)[1];
	MonoVTable *vtable;
	gpointer field_value;
	
	if (strstr (mono_type_full_name (mono_class_get_type (klass)), "`"))
		return;

	while ((field = mono_class_get_fields (klass, &iter)) != NULL) {
		if (mono_field_get_flags (field) & 0x0010 /*FIELD_ATTRIBUTE_STATIC*/) {
			// Dump the class only if it has static fields
			if (!added) {
				outfile_writer_dump_object_begin (p->dumpfile_writer, NULL, klass);
				vtable = mono_class_vtable (domain, klass);
				added = TRUE;
			}
			MonoType* field_type = mono_field_get_type (field);
			
			if (MONO_TYPE_IS_REFERENCE (field_type)) {
				mono_field_static_get_value (vtable, field, &field_value);
				if (field_value) {
					outfile_writer_dump_object_add_reference (p->dumpfile_writer, field_value, field);
				}
			} else {
				MonoClass *fclass = mono_class_from_mono_type (field_type);
				if (fclass && mono_class_is_valuetype (fclass)) {
					if (g_hash_table_lookup (p->exclude_class_hash, fclass))
						continue;
					int size = mono_class_value_size (fclass, NULL);
					if (size >= sizeof(gpointer) && fclass != klass) {
						// Get the object value and scan it
						char* vop = g_malloc (size);
						mono_field_static_get_value (vtable, field, vop);
						// Recursively scan the object
						heap_scan_object (p, (MonoObject*)(vop - sizeof(MonoObject)), fclass, field);
						g_free (vop);
					}
				}
			}
		}
	}
	if (added)
		outfile_writer_dump_object_end (p->dumpfile_writer);
}

static void
dump_domain_static_fields_fn (MonoDomain *domain, gpointer user_data)
{
	MonoProfiler *p = user_data;
	gpointer data [2];
	data [0] = p;
	data [1] = domain;
	g_hash_table_foreach (p->work_class_hash, dump_static_fields_fn, &data);
}

static void
clone_hash_table_fn (gpointer key, gpointer value, gpointer user_data)
{
	GHashTable *hash = (GHashTable*) user_data;
	g_hash_table_insert (hash, key, value);
}

static void
heap_shot_dump_object_map (MonoProfiler *p)
{
	FILE* dfile;

	mono_gc_collect (0);
	mono_mutex_lock (&p->dump_lock);
	
	// Make a copy of the hashtables which collect object and type data,
	// to avoid deadlocks while inspecting the data
	mono_mutex_lock (&p->lock);
	
	p->work_objects_hash = g_hash_table_new (NULL, NULL);
	p->work_class_hash = g_hash_table_new (NULL, NULL);
	g_hash_table_foreach (p->objects_hash, clone_hash_table_fn, p->work_objects_hash);
	g_hash_table_foreach (p->class_hash, clone_hash_table_fn, p->work_class_hash);
	
	mono_mutex_unlock (&p->lock);
	
	gchar* fname = g_strdup_printf ("%s_%d.omap", p->out_file_name, p->dump_count);
	g_print ("Dumping object map to file '%s'\n", fname);
	p->dumpfile_writer = outfile_writer_open_objectmap (fname);
	
	// Dump object information
	g_hash_table_foreach (p->work_objects_hash, heap_scan_fn, p);
	
	p->dump_count++;

	// Dump static field references for each domain
	// This can cause new object allocations
	mono_domain_foreach (dump_domain_static_fields_fn, p);
	
	outfile_writer_close (p->dumpfile_writer);

	dfile = fopen ("/tmp/heap-shot-dump", "w");
	fputs (g_get_current_dir(), dfile);
	fputs ("/", dfile);
	fputs (fname, dfile);
	fclose (dfile);

	g_hash_table_destroy (p->work_objects_hash);
	g_hash_table_destroy (p->work_class_hash);

	g_free (fname);
	
	g_print ("done\n");
	
	mono_mutex_unlock (&p->dump_lock);
}

static void
heap_shot_load_class_func (MonoProfiler *p, MonoClass *klass, int result)
{
	mono_mutex_lock (&p->lock);
	g_hash_table_insert (p->class_hash, klass, klass);
	mono_mutex_unlock (&p->lock);
}

static void
heap_shot_unload_class_func (MonoProfiler *p, MonoClass *klass)
{
	mono_mutex_lock (&p->lock);
	g_hash_table_remove (p->class_hash, klass);
	mono_mutex_unlock (&p->lock);
}

void
mono_profiler_startup (const char *desc)
{
	MonoProfiler *p;

	const char *outfilename;

	g_assert (! strncmp (desc, "heap-shot", 9));

	outfilename = strchr (desc, ':');
	if (outfilename == NULL)
		outfilename = "outfile";
	else {
		// Advance past the : and use the rest as the name.
		++outfilename;
	}

	g_print ("*** Running with heap-shot ***\n");
	
	mono_profiler_install_allocation (heap_shot_alloc_func);
	mono_profiler_install_gc (heap_shot_gc_func, NULL);
	mono_profiler_install_class (NULL, heap_shot_load_class_func, heap_shot_unload_class_func, NULL);
	mono_profiler_set_events (MONO_PROFILE_ALLOCATIONS | MONO_PROFILE_GC | MONO_PROFILE_CLASS_EVENTS);
	
	p = create_mono_profiler (outfilename);
	
	mono_profiler_install (p, NULL);
}

